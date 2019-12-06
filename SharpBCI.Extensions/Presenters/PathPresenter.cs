using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Extensions.Windows;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = SharpBCI.Extensions.Data.Path;

namespace SharpBCI.Extensions.Presenters
{

    public class PathPresenter : IPresenter
    {

        public enum PathType
        {
            File, Directory
        }

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly PathType _pathType;

            private readonly bool _checkExistence;

            private readonly TextBox _pathTextBox;

            private readonly Button _browseButton;

            public Adapter(IParameterDescriptor parameter, PathType pathType, bool checkExistence, TextBox pathTextBox, Button browseButton)
            {
                _parameter = parameter;
                _pathType = pathType;
                _checkExistence = checkExistence;
                _pathTextBox = pathTextBox;
                _browseButton = browseButton;
            }

            public object GetValue()
            {
                var path = string.IsNullOrWhiteSpace(_pathTextBox.Text) ? null : new Path(_pathTextBox.Text);
                if (path != null && _checkExistence)
                {
                    switch (_pathType)
                    {
                        case PathType.File:
                            if (!File.Exists(path.Value)) throw new Exception("file not exists");
                            break;
                        case PathType.Directory:
                            if (!Directory.Exists(path.Value)) throw new Exception("directory not exists");
                            break;
                    }
                }
                return _parameter.IsValidOrThrow(path);
            }

            public void SetValue(object value) => _pathTextBox.Text = ((Path)value)?.Value ?? "";

            public void SetEnabled(bool value)
            {
                _pathTextBox.IsEnabled = value;
                if (_browseButton != null) _browseButton.IsEnabled = value;
            }

            public void SetValid(bool value) => _pathTextBox.Background = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;

        }

        public static readonly NamedProperty<string> FilterProperty = new NamedProperty<string>("Filter", FileUtils.AllFileFilter);

        public static readonly NamedProperty<PathType> PathTypeProperty = new NamedProperty<PathType>("PathType", PathType.File);

        public static readonly NamedProperty<bool> ShowSelectorProperty = new NamedProperty<bool>("ShowSelector", true);

        public static readonly NamedProperty<bool> CheckExistenceProperty = new NamedProperty<bool>("CheckExistence", true);

        public static readonly PathPresenter Instance = new PathPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var pathType = PathTypeProperty.Get(param.Metadata);
            var checkExistence = CheckExistenceProperty.Get(param.Metadata);
            var container = new Grid();
            var textBox = new TextBox {MaxLength = 256};
            textBox.TextChanged += (sender, args) => updateCallback();
            container.Children.Add(textBox);

            Button button = null;
            if (ShowSelectorProperty.Get(param.Metadata))
            {
                button = new Button {Content = "...", HorizontalAlignment = HorizontalAlignment.Right, Width = 25};
                textBox.Margin = new Thickness {Right = ViewConstants.MinorSpacing + button.Width};
                button.Click += (sender, args) =>
                {
                    switch (pathType)
                    {
                        case PathType.File:
                            var openFileDialog = new OpenFileDialog
                            {
                                Title = $"Select File: {param.Name}",
                                Multiselect = false,
                                CheckFileExists = checkExistence,
                                Filter = FilterProperty.Get(param.Metadata),
                            };
                            if (!textBox.Text.IsBlank()) openFileDialog.InitialDirectory = new FileInfo(textBox.Text).Directory?.FullName ?? "";
                            if ((bool) openFileDialog.ShowDialog(Window.GetWindow(button))) textBox.Text = openFileDialog.FileName;
                            break;
                        case PathType.Directory:
                            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                            {
                                if (!textBox.Text.IsBlank()) dialog.SelectedPath = textBox.Text;
                                var result = dialog.ShowDialog();
                                if (result == System.Windows.Forms.DialogResult.OK) textBox.Text = dialog.SelectedPath;
                            }

                            break;
                    }
                };
                container.Children.Add(button);
            }
            return new PresentedParameter(param, container, new Adapter(param, pathType, checkExistence, textBox, button));
        }

    }
}