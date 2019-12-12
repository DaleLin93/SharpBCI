using System;
using JetBrains.Annotations;

namespace SharpBCI.Core.IO
{

    public interface IComponent
    {

        [NotNull] Type AcceptType { get; }

    }

    public interface IPriorityComponent : IComponent, IPriority { }

}
