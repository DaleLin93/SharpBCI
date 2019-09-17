using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using Microsoft.Win32;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class UriPresenter : IPresenter
    {

        public static readonly NamedProperty<string[]> SupportedSchemesProperty = new NamedProperty<string[]>("SupportedSchemes");

        public static readonly NamedProperty<bool> ShowFileSelectorProperty = PathPresenter.ShowSelectorProperty;

        public static readonly NamedProperty<string> FileFilterProperty = PathPresenter.FilterProperty;

        public static readonly NamedProperty<bool> CheckFileExistenceProperty = PathPresenter.CheckExistenceProperty;

        public static readonly UriPresenter Instance = new UriPresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            var container = new Grid();
            var checkFileExistence = CheckFileExistenceProperty.Get(param.Metadata);

            var textBox = new TextBox {MaxLength = 256};
            textBox.TextChanged += (sender, args) => updateCallback();
            container.Children.Add(textBox);

            var supportedSchemes = new HashSet<string>((SupportedSchemesProperty.GetOrDefault(param.Metadata) ?? EmptyArray<string>.Instance).Select(scheme => scheme.ToLowerInvariant()));

            Button button = null;
            if ((!supportedSchemes.Any() || supportedSchemes.Contains("file")) && ShowFileSelectorProperty.GetOrDefault(param.Metadata))
            {
                button = new Button {Content = "...", HorizontalAlignment = HorizontalAlignment.Right, Width = 25};
                textBox.Margin = new Thickness {Right = ViewConstants.MinorSpacing + button.Width};
                button.Click += (sender, args) =>
                {
                    var dialog = new OpenFileDialog
                    {
                        Title = $"Select File: {param.Name}",
                        Multiselect = false,
                        CheckFileExists = CheckFileExistenceProperty.Get(param.Metadata),
                        Filter = FileFilterProperty.Get(param.Metadata),
                    };
                    if (!textBox.Text.IsBlank())
                    {
                        Uri uri = null;
                        try
                        {
                            uri = new Uri(textBox.Text, UriKind.RelativeOrAbsolute);
                        }
                        catch (Exception)
                        {
                            /* ignored */
                        }

                        if (uri?.IsFile ?? false)
                        {
                            var localPath = uri.LocalPath;
                            dialog.InitialDirectory = new FileInfo(localPath).Directory?.FullName ?? "";
                        }
                    }

                    if ((bool) dialog.ShowDialog(window))
                        textBox.Text = "file:\\" + dialog.FileName;
                };
                container.Children.Add(button);
            }

            void Setter(object uri) => textBox.Text = uri?.ToString() ?? "";
            object Getter() => new Uri(textBox.Text);
            bool Validator(object val)
            {
                if (val is Uri uri)
                {
                    if (!supportedSchemes.Any() && !supportedSchemes.Contains(uri.Scheme.ToLowerInvariant())) return false;
                    if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase) && checkFileExistence && !File.Exists(uri.LocalPath)) return false;
                }
                return param.IsValid(val);
            }

            void Updater(ParameterStateType state, bool value)
            {
                switch (state)
                {
                    case ParameterStateType.Enabled:
                        textBox.IsEnabled = value;
                        if (button != null) button.IsEnabled = value;
                        break;
                    case ParameterStateType.Valid:
                        textBox.Background = value ? Brushes.Transparent : new SolidColorBrush(ViewConstants.InvalidColor);
                        break;
                }
            }
            return new PresentedParameter(param, container, new PresentedParameter.ParamDelegates(Getter, Setter, Validator, Updater));
        }

    }
}