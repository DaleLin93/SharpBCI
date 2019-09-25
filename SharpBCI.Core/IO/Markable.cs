using JetBrains.Annotations;

namespace SharpBCI.Core.IO
{

    /// <summary>
    /// The markable interface defined a mark supporting protocol.
    /// </summary>
    public interface IMarkable
    {

        long Mark([CanBeNull] string label, int mark);

    }

    public static class MarkableExt
    {

        public static long Mark(this IMarkable markable, int mark) => markable.Mark(null, mark);

    }

}