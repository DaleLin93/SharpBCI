using System;

namespace SharpBCI.Extensions.Windows
{

    public class LayoutChangedEventArgs : EventArgs
    {

        public static readonly LayoutChangedEventArgs Initialization = new LayoutChangedEventArgs(true);

        public static readonly LayoutChangedEventArgs NonInitialization = new LayoutChangedEventArgs(false);

        private LayoutChangedEventArgs(bool isInitialization) => IsInitialization = isInitialization;

        public bool IsInitialization { get; }

    }
}
