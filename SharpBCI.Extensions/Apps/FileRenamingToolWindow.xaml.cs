using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarukoLib.IO;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Apps
{

    [AppEntry("File Renaming Tool")]
    public class FileRenamingToolAppEntry : IAppEntry
    {

        public void Run() => new FileRenamingToolWindow().Show();

    }

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for FileRenamingToolWindow.xaml
    /// </summary>
    public partial class FileRenamingToolWindow
    {

        private class Node
        {

            protected Node(string parent, string name)
            {
                Parent = parent;
                Name = name;
            }

            internal string Parent { get; }

            internal string Name { get; }

        }

        private class UpwardNode : Node
        {

            public UpwardNode(string parent) : base(parent, null) { }

            public override string ToString() => "..";

        }

        private class DirectoryNode : Node
        {

            public DirectoryNode(string parent, string name) : base(parent, name) { }

            public override string ToString() => $"[ {Name} ]";

        }

        private class FileNode : Node
        {

            public FileNode(string parent, string name, IEnumerable<string> extensions) : base(parent, name) => 
                Extensions = new ReadOnlyCollection<string>(extensions.Distinct().ToArray());

            internal IReadOnlyList<string> Extensions { get; }

            public override string ToString()
            {
                var nameWithExt = Name;
                if (Extensions.Count > 1) nameWithExt += $".* ({Extensions.Count})";
                else if (Extensions.Count == 1 && !string.IsNullOrWhiteSpace(Extensions[0])) nameWithExt += Extensions[0];
                return nameWithExt;
            }
        }

        private Regex _regex;

        public FileRenamingToolWindow() => InitializeComponent();

        public static string GetFileNameWithoutSuffix(string path)
        {
            if (path == null) return null;
            int length;
            if ((length = path.LastIndexOf('.')) == -1) length = path.Length;
            if (length == 0) return string.Empty;
            var sharpAt = path.LastIndexOf('#', length - 1);
            if (sharpAt >= 0 && int.TryParse(path.Substring(sharpAt + 1, length - sharpAt - 1), out _)) return path.Substring(0, sharpAt);
            return path.Substring(0, length);
        }

        private void UpdateFilter()
        {
            var options = (CaseInsensitiveCheckBox.IsChecked ?? false ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.Compiled;
            Regex regex = null;
            try
            {
                regex = string.IsNullOrWhiteSpace(FilterPatternTextBox.Text) ? null : new Regex($"^{FilterPatternTextBox.Text}$", options);
            }
            catch (Exception)
            {
                // ignored
            }
            if (regex == _regex) return;
            _regex = regex;
            UpdateFileList();
        }

        private void UpdateFileList()
        {
            if (!IsLoaded) return;
            var searchingDirectory = DirectoryTextBox.Text;
            if (!Directory.Exists(searchingDirectory))
            {
                FilesListBox.ItemsSource = new[] { "<Invalid Directory>" };
                return;
            }

            var regex = _regex;
            var list = new LinkedList<object>();
            var parentDirectory = Directory.GetParent(searchingDirectory);
            if (parentDirectory != null) list.AddLast(new UpwardNode(parentDirectory.FullName));
            var directoryNames = Directory.EnumerateDirectories(searchingDirectory);
            if (regex != null) directoryNames = directoryNames.Where(d => regex.IsMatch(Path.GetFileName(d) ?? ""));
            foreach (var directory in directoryNames) list.AddLast(new DirectoryNode(searchingDirectory, Path.GetFileName(directory)));
            var filePaths = Directory.EnumerateFiles(DirectoryTextBox.Text);
            if (FileGroupingCheckBox.IsChecked ?? false)
            {
                var fileNameAndSuffixes = new Dictionary<string, LinkedList<string>>();
                foreach (var file in filePaths)
                {
                    var fileName = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(fileName)) continue;
                    var fileNameWithoutSuffix = GetFileNameWithoutSuffix(fileName);
                    var suffix = fileName.Substring(fileNameWithoutSuffix.Length);
                    if (regex != null && !regex.IsMatch(fileNameWithoutSuffix)) continue;
                    if (!fileNameAndSuffixes.TryGetValue(fileNameWithoutSuffix, out var filePathList))
                        fileNameAndSuffixes[fileNameWithoutSuffix] = filePathList = new LinkedList<string>();
                    filePathList.AddLast(suffix);
                }
                foreach (var entry in fileNameAndSuffixes) list.AddLast(new FileNode(searchingDirectory, entry.Key, entry.Value));
            }
            else
            {
                if (regex != null) filePaths = filePaths.Where(f => regex.IsMatch(Path.GetFileNameWithoutExtension(f) ?? ""));
                foreach (var file in filePaths)
                    list.AddLast(new FileNode(searchingDirectory, Path.GetFileNameWithoutExtension(file), new[] { Path.GetExtension(file) }));
            }
            FileItemCountTextBlock.Text = $"({list.Count} items)";
            FilesListBox.ItemsSource = list;
        }

        private void UpdateNewName()
        {
            if (FilesListBox.SelectedItem is FileNode fileNode)
                NewNameTextBox.Text = _regex == null || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text)
                    ? fileNode.Name : _regex.Replace(fileNode.Name, RenamePatternTextBox.Text);
            else
                NewNameTextBox.Text = "";
            NewNameTextBox.Text = NewNameTextBox.Text.RemoveInvalidCharacterForFileName();
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e) => DirectoryTextBox.Text = FileUtils.ExecutableDirectory;

        private void FilterPatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateFilter();

        private void RenamePatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateNewName();

        private void DirectoryTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateFileList();

        private void CaseInsensitiveCheckBox_OnIsCheckedChanged(object sender, RoutedEventArgs e) => UpdateFilter();

        private void FileGroupingCheckBox_OnIsCheckedChanged(object sender, RoutedEventArgs e) => UpdateFileList();

        private void FilesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateNewName();

        private void FilesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            switch (FilesListBox.SelectedItem)
            {
                case DirectoryNode directoryNode:
                    DirectoryTextBox.Text = Path.Combine(directoryNode.Parent, directoryNode.Name);
                    break;
                case UpwardNode upwardNode:
                    DirectoryTextBox.Text = upwardNode.Parent;
                    break;
            }
        }

        private void RenameSelectedFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(FilesListBox.SelectedItem is FileNode fileNode)) return;
            if (string.IsNullOrWhiteSpace(NewNameTextBox.Text))
            {
                MessageBox.Show("New name cannot be empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var fileCount = fileNode.Extensions.Count;
            if (fileCount <= 0 || Equals(fileNode.Name, NewNameTextBox.Text)) return;
            var namePairs = new LinkedList<Pair<string>>();
            foreach (var fileNodeExtension in fileNode.Extensions)
            {
                var filePath = fileNode.Name;
                var newPath = NewNameTextBox.Text;
                if (fileNodeExtension != null)
                {
                    filePath += fileNodeExtension;
                    newPath += fileNodeExtension;
                }
                namePairs.AddLast(new Pair<string>(filePath, newPath));
            }
            var stringBuilder = new StringBuilder(256).Append("Are you sure to rename following ").Append(namePairs.Count).Append(" files?\n");
            foreach (var namePair in namePairs)
                stringBuilder.Append(" ").Append(namePair.Left).Append(" => ").Append(namePair.Right).Append('\n');
            var result = MessageBox.Show(stringBuilder.ToString(), "Rename Files",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            foreach (var namePair in namePairs)
                try
                {
                    File.Move(Path.Combine(fileNode.Parent, namePair.Left), Path.Combine(fileNode.Parent, namePair.Right));
                }
                catch (Exception)
                {
                    /* Message box */
                    break;
                }
                finally
                {
                    UpdateFileList();
                }
        }

        private void RenameAllListedFilesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FilterPatternTextBox.Text) || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text))
            {
                MessageBox.Show("Filter pattern and rename pattern cannot be empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var renameList = new LinkedList<Tuple<string, Pair<string>>>();
            foreach (var fileNode in FilesListBox.ItemsSource.OfType<FileNode>())
            {
                var fileCount = fileNode.Extensions.Count;
                var fileName = fileNode.Name;
                var newName = _regex.Replace(fileName, RenamePatternTextBox.Text);
                if (fileCount <= 0 || Equals(fileName, newName)) continue;
                foreach (var fileNodeExtension in fileNode.Extensions)
                {
                    if (fileNodeExtension != null)
                    {
                        fileName += fileNodeExtension;
                        newName += fileNodeExtension;
                    }
                    renameList.AddLast(new Tuple<string, Pair<string>>(fileNode.Parent, new Pair<string>(fileName, newName)));
                }
            }
            if (renameList.Count <= 0)
            {
                MessageBox.Show("No file will be renamed!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var stringBuilder = new StringBuilder(256).Append("Are you sure to rename following ").Append(renameList.Count).Append(" files?\n");
            foreach (var item in renameList)
                stringBuilder.Append(" ").Append(item.Item2.Left).Append(" => ").Append(item.Item2.Right).Append('\n');
            var result = MessageBox.Show(stringBuilder.ToString(), "Rename Files",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            var renameFailedList = new LinkedList<Tuple<string, Pair<string>>>();
            foreach (var item in renameList)
                try
                {
                    File.Move(Path.Combine(item.Item1, item.Item2.Left), Path.Combine(item.Item1, item.Item2.Right));
                }
                catch (Exception)
                {
                    renameFailedList.AddLast(item);
                }
            if (renameFailedList.Count > 0) {
                var failedMessageBuilder = new StringBuilder(256).Append("Failed to rename following ").Append(renameFailedList.Count).Append(" files:\n");
                foreach (var failedItem in renameFailedList)
                    failedMessageBuilder.Append(" ").Append(failedItem.Item2.Left).Append(" => ").Append(failedItem.Item2.Right).Append('\n');
                MessageBox.Show(failedMessageBuilder.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateFileList();
        }

    }
}
