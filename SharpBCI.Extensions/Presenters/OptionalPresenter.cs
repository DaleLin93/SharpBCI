using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace SharpBCI.Extensions.Presenters
{

    public class OptionalPresenter : IPresenter
    {

        public static readonly OptionalPresenter Instance = new OptionalPresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            var container = new Grid();
            var checkbox = new CheckBox();
            container.Children.Add(checkbox);
            // TODO
            return null;
        }

    }
}