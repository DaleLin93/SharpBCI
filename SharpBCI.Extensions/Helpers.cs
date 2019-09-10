using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using SharpBCI.Extensions.Data;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SharpBCI.Extensions
{

    public class GroupHeader : Grid
    {

        private readonly Rectangle _separatorRectangle;

        private readonly TextBlock _headerTextBlock;

        public GroupHeader()
        {
            Height = ViewConstants.SeparatorRowHeight;
            Background = new SolidColorBrush(ViewConstants.GroupHeaderColor);
            Children.Add(_separatorRectangle = new Rectangle {Margin = new Thickness {Left = 10, Right = 10, Top = 7}});
            Children.Add(_headerTextBlock = new TextBlock {Margin = new Thickness {Left = 15, Top = 2}, IsHitTestVisible = false, Visibility = Visibility.Hidden});
        }

        public Style SeparatorStyle
        {
            get => _separatorRectangle.Style;
            set => _separatorRectangle.Style = value;
        }

        public Style HeaderTextStyle
        {
            get => _headerTextBlock.Style;
            set => _headerTextBlock.Style = value;
        }

        public string Header
        {
            get => _headerTextBlock.Text;
            set
            {
                _headerTextBlock.Text = value;
                _headerTextBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Hidden : Visibility.Visible;
            }
        }

        public string Description
        {
            get => ToolTip?.ToString();
            set => ToolTip = value;
        }

    }

    public static class ParameterPersistentExt
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(ParameterPersistentExt));

        public static IDictionary<string, string> SerializeParams(this IEnumerable<IParameterDescriptor> parameters, IReadonlyContext context)
        {
            if (context == null) return null;
            var @params = new Dictionary<string, string>();
            foreach (var p in parameters)
                if (context.TryGet(p, out var val))
                    try { @params[p.Key] = p.SerializeParam(val); }
                    catch (Exception e) { Logger.Warn("SerializeParams", e, "param", p.Key, "value", val); }
            return @params;
        }

        public static IContext DeserializeParams(this IEnumerable<IParameterDescriptor> parameters, IDictionary<string, string> input)
        {
            if (input == null) return null;
            var context = new Context();
            foreach (var p in parameters)
                if (input.ContainsKey(p.Key))
                    try { context.Set(p, p.DeserializeParam(input[p.Key])); }
                    catch (Exception e) { Logger.Warn("DeserializeParams", e, "param", p.Key, "value", input[p.Key]); }
            return context;
        }

        public static string SerializeParam(this IParameterDescriptor parameter, object value)
        {
            if (value == null) return null;
            if (parameter.TypeConverter != null)
                return JsonUtils.Serialize(parameter.TypeConverter.ConvertForward(value));
            if (typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType))
            {
                var factory = parameter.GetParameterizedObjectFactory();
                var context = factory.Parse(parameter, (IParameterizedObject)value);
                var output = new Dictionary<string, string>();
                foreach (var p in factory.GetParameters(parameter))
                    if (context.TryGet(p, out var val))
                        output[p.Key] = SerializeParam(p, val);
                return JsonUtils.Serialize(output);
            }
            return JsonUtils.Serialize(value);
        }

        public static object DeserializeParam(this IParameterDescriptor parameter, string value)
        {
            var typeConverter = parameter.TypeConverter;
            if (typeConverter != null)
                return typeConverter.ConvertBackward(JsonUtils.Deserialize(value, typeConverter.OutputType));
            if (typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType))
            {
                var factory = parameter.GetParameterizedObjectFactory();
                var context = new Context();
                var strParams = JsonUtils.Deserialize<Dictionary<string, string>>(value);
                foreach (var p in factory.GetParameters(parameter))
                    context.Set(p, DeserializeParam(p, strParams[p.Key]));
                return factory.Create(parameter, context);
            }
            return value == null ? null : JsonUtils.Deserialize(value, parameter.ValueType);
        }

    }

    public static class ViewHelper
    {

        public static GroupHeader CreateGroupHeader(this FrameworkElement element, string header, string description) =>
            new GroupHeader
            {
                SeparatorStyle = element.TryFindResource("ParamGroupSeparator") as Style,
                HeaderTextStyle = element.TryFindResource("ParamGroupHeader") as Style,
                Header = header,
                Description = description
            };

        public static StackPanel AddGroupPanel(this Panel parent, string header, string description, int depth = 0)
        {
            var stackPanel = new StackPanel();
            if (depth > 0) stackPanel.Margin = new Thickness { Left = ViewConstants.Intend * depth };
            stackPanel.Children.Add(CreateGroupHeader(parent, header, description));
            parent.Children.Add(stackPanel);
            return stackPanel;
        }

        public static Grid AddRow(this Panel parent, string label, UIElement rightPart, uint rowHeight = 0) =>
            AddRow(parent, label == null ? null : new TextBlock {Text = label, Style = parent.TryFindResource("LabelText") as Style}, rightPart, rowHeight);

        public static Grid AddRow(this Panel parent, UIElement leftPart, UIElement rightPart, uint rowHeight = 0)
        {
            var row = new Grid {Margin = ViewConstants.RowMargin};
            if (rowHeight > 0) row.Height = rowHeight;
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 300 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ViewConstants.MajorSpacing) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            row.Children.Add(leftPart);
            Grid.SetColumn(leftPart, 0);
            row.Children.Add(rightPart);
            Grid.SetColumn(rightPart, 2);
            parent.Children.Add(row);
            return row;
        }

        public static TextBlock CreateParamNameTextBlock(this FrameworkElement element, IParameterDescriptor param) => new TextBlock
        {
            Style = (Style)element.TryFindResource("LabelText"),
            Text = param.Name + (param.Unit == null ? "" : $" ({param.Unit})"),
            ToolTip = $"Key: {param.Key}\nValue Type: {param.ValueType.GetFriendlyName()}{(param.Description == null ? "" : "\n" + param.Description)}",
        };

    }

    public static class ConversionHelper
    {

        public static T AsType<T>(this string strVal) => (T)typeof(T).Parse(strVal);

        public static object Parse(this Type type, string strVal)
        {
            if (type == typeof(string)) return strVal;

            var nullableType = type.IsNullableType(out var underlyingType);
            var actualType = nullableType ? underlyingType : type;

            if (!actualType.IsPrimitive)
                throw new ArgumentException("type is not supported, type: " + type.FullName);

            if (strVal?.IsEmpty() ?? true)
                if (nullableType)
                    return null;
                else
                    throw new ArgumentException("cannot parse null string, type: " + type.FullName);

            if (actualType == typeof(char))
                return strVal[0];
            if (actualType == typeof(byte))
                return byte.Parse(strVal);
            if (actualType == typeof(sbyte))
                return sbyte.Parse(strVal);
            if (actualType == typeof(short))
                return short.Parse(strVal);
            if (actualType == typeof(ushort))
                return ushort.Parse(strVal);
            if (actualType == typeof(int))
                return int.Parse(strVal);
            if (actualType == typeof(uint))
                return uint.Parse(strVal);
            if (actualType == typeof(ulong))
                return ulong.Parse(strVal);
            if (actualType == typeof(float))
                return float.Parse(strVal);
            if (actualType == typeof(double))
                return double.Parse(strVal);
            if (actualType == typeof(decimal))
                return decimal.Parse(strVal);

            throw new Exception("unreachable statement");
        }

        public static string ValueToString(this IParameterDescriptor parameter, object val)
        {
            if (parameter.TypeConverter != null)
                val = parameter.TypeConverter.ConvertForward(val);
            if (val == null) return "<<null>>";
            var type = val.GetType();
            if (type.IsArray)
            {
                if (type.GetArrayRank() == 1 && (type.GetElementType()?.IsPrimitive ?? false))
                {
                    var stringBuilder = new StringBuilder();
                    var array = (Array)val;
                    for (var i = 1; i <= array.Length; i++)
                    {
                        stringBuilder.Append(array.GetValue(i - 1));
                        if (i != array.Length)
                            stringBuilder.Append(' ');
                    }
                    return stringBuilder.ToString();
                }
                throw new NotSupportedException();
            }
            if (val is IDescribable describable)
                return describable.GetShortDescription();
            return val.ToString();
        }

        public static object ParseValue(this IParameterDescriptor parameter, string strVal)
        {
            if ("<<null>>".Equals(strVal)) return null;
            if (parameter.TypeConverter != null)
            {
                var parsed = ParseValue(parameter.TypeConverter.OutputType, strVal);
                return parameter.TypeConverter.ConvertBackward(parsed);
            }
            return ParseValue(parameter.ValueType, strVal);
        }

        /// <summary>
        /// Throws Exception
        /// </summary>
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static object ParseValue(Type type, string strVal)
        {
            if ("<<null>>".Equals(strVal)) return null;
            if (type.IsArray)
                if (type.GetArrayRank() == 1)
                {
                    strVal = strVal.Trim();
                    var substrings = strVal.Split(' ').Where(str => !str.IsBlank()).ToArray();
                    var array = Array.CreateInstance(type.GetElementType(), substrings.Length);
                    for (var i = 0; i < substrings.Length; i++)
                        array.SetValue(ParseValue(type.GetElementType(), substrings[i]), i);
                    return array;
                }
                else
                    throw new NotSupportedException("Only 1D-array was supported");
            return type.Parse(strVal);
        }

    }

}
