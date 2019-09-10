using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Windows
{
    public class ContextChangedEventArgs : EventArgs
    {

        public ContextChangedEventArgs(IReadonlyContext context) => Context = context;

        public IReadonlyContext Context { get; }

    }

}