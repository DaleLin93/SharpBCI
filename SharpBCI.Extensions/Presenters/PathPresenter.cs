using System;
using System.Diagnostics.CodeAnalysis;
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

        public static readonly NamedProperty<string> FilterProperty = new NamedProperty<string>("Filter", FileUtils.AllFileFilter);

        public static readonly NamedProperty<PathType> PathTypeProperty = new NamedProperty<PathType>("PathType", PathType.File);

        public static readonly NamedProperty<bool> ShowSelectorProperty = new NamedProperty<bool>("ShowSelector", true);

        public static readonly NamedProperty<bool> CheckExistenceProperty = new NamedProperty<bool>("CheckExistence", true);

        public static readonly PathPresenter Instance = new PathPresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
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
            void Setter(object file) => textBox.Text = ((Path) file)?.Value ?? "";
            object Getter() => new Path(textBox.Text);
            bool Validate(object value)
            {
                if (!(value is Path file)) return false;
                if (checkExistence)
                {
                    switch (pathType)
                    {
                        case PathType.File:
                            if (!File.Exists(file.Value)) return false;
                            break;
                        case PathType.Directory:
                            if (!Directory.Exists(file.Value)) return false;
                            break;
                    }
                } 
                return param.IsValid(value);
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
            return new PresentedParameter(param, container, new PresentedParameter.ParamDelegates(Getter, Setter, Validate, Updater));
        }

    }
}