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
                if (Extensions.Count > 1) nameWithExt += ".*";
                else if (Extensions.Count == 1 && !string.IsNullOrWhiteSpace(Extensions[0])) nameWithExt += Extensions[0];
                return nameWithExt;
            }
        }

        private Regex _regex = null;

        public FileRenamingToolWindow() => InitializeComponent();

        private void UpdateFileList()
        {
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
            var fileNames = Directory.EnumerateFiles(DirectoryTextBox.Text);
            if (regex != null) fileNames = fileNames.Where(f => regex.IsMatch(Path.GetFileNameWithoutExtension(f) ?? ""));
            var fileNameDict = new Dictionary<string, LinkedList<string>>();
            foreach (var file in fileNames)
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(fileNameWithoutExt)) continue;
                if (!fileNameDict.TryGetValue(fileNameWithoutExt, out var filePathList))
                    fileNameDict[fileNameWithoutExt] = filePathList = new LinkedList<string>();
                filePathList.AddLast(file);
            }
            foreach (var entry in fileNameDict) list.AddLast(new FileNode(searchingDirectory, entry.Key, entry.Value.Select(Path.GetExtension)));
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

        private void FilterPatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            Regex regex = null;
            try
            {
                regex = string.IsNullOrWhiteSpace(FilterPatternTextBox.Text) ? null : new Regex($"^{FilterPatternTextBox.Text}$");
            }
            catch (Exception)
            {
                // ignored
            }
            if (regex == _regex) return;
            _regex = regex;
            UpdateFileList();
        }

        private void RenamePatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateNewName();

        private void DirectoryTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateFileList();

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
            if (FilesListBox.SelectedItem is FileNode fileNode)
            {
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
                try
                {
                    foreach (var namePair in namePairs)
                        File.Move(Path.Combine(fileNode.Parent, namePair.Left), Path.Combine(fileNode.Parent, namePair.Right));
                }
                finally
                {
                    UpdateFileList();
                }
            }
        }

        private void RenameAllListedFilesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FilterPatternTextBox.Text) || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text))
            {
                MessageBox.Show("Filter pattern and rename pattern cannot be empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var namePairs = new LinkedList<Tuple<string, Pair<string>>>();
            foreach (var fileNode in FilesListBox.ItemsSource.OfType<FileNode>())
            {
                var fileCount = fileNode.Extensions.Count;
                var newName = _regex.Replace(fileNode.Name, RenamePatternTextBox.Text);
                if (fileCount <= 0 || Equals(fileNode.Name, newName)) return;
                foreach (var fileNodeExtension in fileNode.Extensions)
                {
                    var filePath = fileNode.Name;
                    var newPath = newName;
                    if (fileNodeExtension != null)
                    {
                        filePath += fileNodeExtension;
                        newPath += fileNodeExtension;
                    }
                    namePairs.AddLast(new Tuple<string, Pair<string>>(fileNode.Parent, new Pair<string>(filePath, newPath)));
                }
            }
            var stringBuilder = new StringBuilder(256).Append("Are you sure to rename following ").Append(namePairs.Count).Append(" files?\n");
            foreach (var namePair in namePairs)
                stringBuilder.Append(" ").Append(namePair.Item2.Left).Append(" => ").Append(namePair.Item2.Right).Append('\n');
            var result = MessageBox.Show(stringBuilder.ToString(), "Rename Files",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                foreach (var namePair in namePairs)
                    File.Move(Path.Combine(namePair.Item1, namePair.Item2.Left), Path.Combine(namePair.Item1, namePair.Item2.Right));
            }
            finally
            {
                UpdateFileList();
            }
        }

    }
}
