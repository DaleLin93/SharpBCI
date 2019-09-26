using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Windows.Input;
using JetBrains.Annotations;
using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Paradigms.Speller
{

    public sealed class KeyDescriptor
    {

        [CanBeNull]
        public readonly char? InputChar;

        [NotNull]
        public readonly string Name;

        [NotNull]
        [JsonIgnore]
        public readonly UnaryOperator<string> Operator;

        public KeyDescriptor(char inputChar, string name = null)
        {
            InputChar = inputChar;
            Name = name ?? inputChar.ToString();
            Operator = input => input + InputChar.Value;
        }

        public KeyDescriptor(string name, UnaryOperator<string> @operator)
        {
            InputChar = null;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

    }

    public static class KeyDescriptors
    {

        public static readonly KeyDescriptor SpaceKey = new KeyDescriptor(' ', "Space");

        public static readonly KeyDescriptor[] NumericKeys = ContinuousKeys(Key.D0, Key.D9); // 10

        public static readonly KeyDescriptor[] LetterKeys = ContinuousKeys(Key.A, Key.Z); // 26

        public static readonly KeyDescriptor[] CommandKeys = {
            new KeyDescriptor("<-", str => str.IsEmpty() ? str : str.Substring(0, str.Length)),
            new KeyDescriptor("Clear", str => "")
        }; // 2

        public static KeyDescriptor[] CharKeys(string chars) => CharKeys(chars.ToCharArray());

        public static KeyDescriptor[] CharKeys(params char[] chars)
        {
            var keys = new KeyDescriptor[chars.Length];
            for (var i = 0; i < chars.Length; i++)
                keys[i] = new KeyDescriptor(chars[i]);
            return keys;
        }

        public static KeyDescriptor[] ContinuousKeys(Key from, Key to)
        {
            var len = Math.Abs(to - from) + 1;
            var keys = new KeyDescriptor[len];
            for (var i = 0; i < len; i++)
            {
                var key = from + i * (from > to ? -1 : +1);
                string keyName;
                if (key >= Key.D0 && key <= Key.D9)
                    keyName = key.ToString().Substring(1);
                else
                    keyName = key.ToString();
                keys[i] = new KeyDescriptor(keyName[0], keyName);
            }
            return keys;
        }

    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public sealed class KeyboardLayout
    {

        public static readonly IDictionary<string, KeyboardLayout> Layouts;

        public static readonly TypeConverter TypeConverter = TypeConverter.Of<KeyboardLayout, string>(l => l.Name, s => Layouts[s]);

        static KeyboardLayout()
        {
            var layouts = new Dictionary<string, KeyboardLayout>();
            void Put(KeyboardLayout layout) => layouts[layout.Name] = layout;
            Put(new KeyboardLayout("Numeric Keyboard", 4, KeyDescriptors.NumericKeys, KeyDescriptors.CommandKeys));
            Put(new KeyboardLayout("Alphabet Keyboard", 9, KeyDescriptors.NumericKeys, KeyDescriptors.LetterKeys));

            Put(new KeyboardLayout("40-classes Keyboard", 8, KeyDescriptors.NumericKeys, KeyDescriptors.LetterKeys, new[] { KeyDescriptors.SpaceKey },
                KeyDescriptors.CharKeys("!-@")));

            Put(new KeyboardLayout("45-classes Keyboard", 9, KeyDescriptors.NumericKeys, KeyDescriptors.LetterKeys, new []{ KeyDescriptors.SpaceKey }, 
                KeyDescriptors.CharKeys(",.?!@&#$")));

            Put(new KeyboardLayout("60-classes Keyboard", 10, KeyDescriptors.NumericKeys, KeyDescriptors.LetterKeys,
                KeyDescriptors.CharKeys(";:,./*-+()\\?\"`~<>{}|=$"), // 22
                KeyDescriptors.CommandKeys));

            Put(new KeyboardLayout("70-classes Keyboard", 10, KeyDescriptors.NumericKeys, KeyDescriptors.LetterKeys,
                new[] {KeyDescriptors.SpaceKey},
                KeyDescriptors.CharKeys(";:,./*-+()\\?\"'!@#$%`~[]<>{}|=^∞"), // 31
                KeyDescriptors.CommandKeys));

            Put(new KeyboardLayout("104-classes Keyboard", 13, KeyDescriptors.NumericKeys,
                KeyDescriptors.CharKeys(";:,./*-+()\\?\"'!@#$%~[]<>∞∫∮∝≈≤≥≌∽"),
                KeyDescriptors.LetterKeys, new[] { KeyDescriptors.SpaceKey }, // 26 + 1
                KeyDescriptors.CharKeys("αβγδεζηθικλμνξοπρστυφχψω"),// 25
                KeyDescriptors.CharKeys("ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ"))); // 9

            Put(new KeyboardLayout("112-classes Keyboard", 14, KeyDescriptors.NumericKeys,
                KeyDescriptors.CharKeys(";:,./*-+()\\?\"'!@#$%~[]<>∞∫∮∝≈≌∽"),
                KeyDescriptors.LetterKeys, new[] { KeyDescriptors.SpaceKey }, // 26 + 1
                KeyDescriptors.CharKeys("αβγδεζηθικλμνξοπρστυφχψω"),// 25
                KeyDescriptors.CharKeys("①②③④⑤⑥⑦⑧⑨⑩"), // 10
                KeyDescriptors.CharKeys("ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ"))); // 9
            Layouts = layouts;
        }

        private KeyboardLayout([NotNull] string name, int columns, params KeyDescriptor[][] keyGroups)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Keys = new List<KeyDescriptor>(keyGroups.SelectMany(group => group));
            Columns = columns;
            Rows = RowCount(Keys.Count, Columns);
        }

        public static int RowCount(int count, int columns) => count / columns + (count % columns > 0 ? 1 : 0);

        [NotNull]
        public string Name { get; }

        [NotNull]
        public ICollection<KeyDescriptor> Keys { get; }

        public int KeyCount => Keys.Count;

        [NotNull]
        public ISet<char> SupportedCharSet
        {
            get
            {
                var charSet = new HashSet<char>();
                foreach (var keyDescriptor in Keys)
                    if (keyDescriptor.InputChar != null)
                        charSet.Add(keyDescriptor.InputChar.Value);
                return charSet;
            }
        }

        public int Columns { get; }

        public int Rows { get; }

        public int[] GetLayoutSize(int overrideColumnNum = 0) => overrideColumnNum <= 0 ? new[] { Rows, Columns } : new[] { RowCount(Keys.Count, overrideColumnNum), overrideColumnNum };

        public string FilterText(string text)
        {
            if (text == null) return null;
            var supportedCharSet = SupportedCharSet;
            var stringBuilder = new StringBuilder(text.Length);
            foreach (var ch in text)
                if (supportedCharSet.Contains(ch))
                    stringBuilder.Append(ch);
            return stringBuilder.ToString();
        }

    }

}