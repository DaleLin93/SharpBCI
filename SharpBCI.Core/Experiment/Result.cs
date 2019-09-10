using System.Collections.Generic;
using System.Text;
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

            public readonly string Title;

            public readonly string Value;

            public Item(string title, string value)
            {
                Title = title;
                Value = value;
            }

            public static bool IsSeparator(Item item) => item == Separator;

        }

        /// <summary>
        /// The result items.
        /// </summary>
        public abstract IEnumerable<Item> Items { get; }

        public virtual void Save(Session session)
        {
            if (!SkipSaveProperty.GetOrDefault(session, false)) return;
            this.JsonSerializeToFile(session.GetDataFileName(FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);
        }

    }

}
