using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Persistence;

namespace SharpBCI.Core.Experiment
{

    public abstract class Result
    {

        private sealed class EmptyResult : Result
        {

            public override IEnumerable<Item> Items => EmptyArray<Item>.Instance;

            public override void Save(Session session) { }

        }

        public const string FileSuffix = ".result";

        /// <summary>
        /// An empty result instance.
        /// </summary>
        public static readonly Result Empty = new EmptyResult();

        /// <summary>
        /// Configurable property of session.
        /// If set to true, the result will not be save automatically at the end of the session.
        /// </summary>
        public static readonly ContextProperty<bool> SkipSaveProperty = new ContextProperty<bool>();

        public class Item
        {

            public static readonly Item Separator = null;

            [NotNull] public readonly string Title;

            [CanBeNull] public readonly string Value;

            public Item([NotNull] string title, [CanBeNull] string value)
            {
                Title = title ?? throw new ArgumentNullException(nameof(title));
                Value = value;
            }

            public static bool IsSeparator([CanBeNull] Item item) => item == Separator;

        }

        /// <summary>
        /// The result items.
        /// </summary>
        [NotNull] public abstract IEnumerable<Item> Items { get; }

        public virtual void Save([NotNull] Session session)
        {
            if (SkipSaveProperty.GetOrDefault(session, false)) return;
            this.JsonSerializeToFile(session.GetDataFileName(FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);
        }

    }

}
