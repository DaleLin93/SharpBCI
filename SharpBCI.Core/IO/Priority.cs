namespace SharpBCI.Core.IO
{

    public enum Priority : sbyte
    {
        Monitor = -1,
        Highest = 0,
        High = 1,
        Normal = 2,
        Low = 3,
        Lowest = 4
    }

    public interface IPriority
    {

        Priority Priority { get; }

    }

}