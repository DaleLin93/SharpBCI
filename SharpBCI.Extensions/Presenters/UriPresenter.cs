using System;
using System.Collections.Generic;
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

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly ISet<string> _supportedSchemes;

            private readonly bool _checkFileExistence;

            private readonly TextBox _uriTextBox;

            private readonly Button _browseButton;

            public Adapter(IParameterDescriptor parameter, ISet<string> supportedSchemes, bool checkFileExistence, TextBox uriTextBox, Button browseButton)
            {
                _parameter = parameter;
                _supportedSchemes = supportedSchemes;
                _checkFileExistence = checkFileExistence;
                _uriTextBox = uriTextBox;
                _browseButton = browseButton;
            }

            public bool IsEnabled
            {
                get => _uriTextBox.IsEnabled;
                set
                {
                    _uriTextBox.IsEnabled = value;
                    if (_browseButton != null) _browseButton.IsEnabled = value;
                }
            }

            public bool IsValid
            {
                get => _uriTextBox.Background != ViewConstants.InvalidColorBrush;
                set => _uriTextBox.Background = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;
            }

            public object Value
            {
                get
                {
                    var uri = new Uri(_uriTextBox.Text);
                    if (!_supportedSchemes.Any() && !_supportedSchemes.Contains(uri.Scheme.ToLowerInvariant())) throw new Exception("unsupported scheme");
                    if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase) && _checkFileExistence && !File.Exists(uri.LocalPath)) throw new Exception("file not exists");
                    return _parameter.IsValidOrThrow(uri);
                }
                set => _uriTextBox.Text = value?.ToString() ?? "";
            }
            
        }

        public static readonly NamedProperty<string[]> SupportedSchemesProperty = new NamedProperty<string[]>("SupportedSchemes");

        public static readonly NamedProperty<bool> ShowFileSelectorProperty = PathPresenter.ShowSelectorProperty;

        public static readonly NamedProperty<string> FileFilterProperty = PathPresenter.FilterProperty;

        public static readonly NamedProperty<bool> CheckFileExistenceProperty = PathPresenter.CheckExistenceProperty;

        public static readonly UriPresenter Instance = new UriPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var container = new Grid();
            var checkFileExistence = CheckFileExistenceProperty.Get(param.Metadata);
            var fileFilter = FileFilterProperty.Get(param.Metadata);

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
                        CheckFileExists = checkFileExistence,
                        Filter = fileFilter,
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

                    if ((bool) dialog.ShowDialog(Window.GetWindow(button)))
                        textBox.Text = "file:\\" + dialog.FileName;
                };
                container.Children.Add(button);
            }
            return new PresentedParameter(param, container, new Adapter(param, supportedSchemes, checkFileExistence, textBox, button));
        }

    }
}