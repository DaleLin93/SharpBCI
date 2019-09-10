namespace SharpBCI.Core.IO
{

    /// <summary>
    /// The markable interface defined a marker supporting protocol.
    /// </summary>
    public interface IMarkable
    {

        long Mark(int marker);

    }

}