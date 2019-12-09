using System;

namespace SharpBCI.Core.IO
{

    public interface IComponent
    {

        Type AcceptType { get; }

    }

    public interface IPriorityComponent : IComponent, IPriority { }

}
