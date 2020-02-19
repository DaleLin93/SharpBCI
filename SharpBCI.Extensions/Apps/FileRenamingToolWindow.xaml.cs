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
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Windows;

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
                Suffixes = new ReadOnlyCollection<string>(extensions.Distinct().ToArray());

            internal IReadOnlyList<string> Suffixes { get; }

            public override string ToString()
            {
                var nameWithExt = Name;
                if (Suffixes.Count > 1) nameWithExt += $".* ({Suffixes.Count})";
                else if (Suffixes.Count == 1 && !string.IsNullOrWhiteSpace(Suffixes[0])) nameWithExt += Suffixes[0];
                return nameWithExt;
            }

        }

        private Regex _regex;

        public FileRenamingToolWindow() => InitializeComponent();

        /// <summary>
        /// Example of <see cref="Path.GetFileNameWithoutExtension"/>: 
        /// <![CDATA[C:\Abc.txt => Abc]]>
        /// Examples of <see cref="GetFileNameWithoutSuffix"/>: 
        /// <![CDATA[C:\Abc.txt => Abc]]>
        /// <![CDATA[C:\Abc#12.txt => Abc]]>
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns>Path without suffix</returns>
        public static string GetFileNameWithoutSuffix(string path)
        {
            if (path == null) return null;
            /* Remove suffix (including '.') */
            int length;
            if ((length = path.LastIndexOf('.')) == -1) length = path.Length;
            if (length == 0) return string.Empty;
            /* Remove numbering (including '#') */
            var sharpAt = path.LastIndexOf('#', length - 1);
            if (sharpAt >= 0 && int.TryParse(path.Substring(sharpAt + 1, length - sharpAt - 1), out _)) 
                return path.Substring(0, sharpAt);
            return path.Substring(0, length);
        }

        public static void Rename([ItemNotNull] params FileUtils.RenamePair[] input) => Rename((IEnumerable<FileUtils.RenamePair>)input);

        public static void Rename([ItemNotNull] IEnumerable<FileUtils.RenamePair> input)
        {
            var items = input.AsReadonlyCollection();

            /* Preconditions */
            if (items.Count <= 0)
            {
                MessageBoxUtils.InfoOk("No file will be renamed!");
                return;
            }

            try
            {
                /* Rename confirmation */
                var msgBuilder = new StringBuilder(256);
                msgBuilder.Append("Are you sure to rename following ").Append(items.Count).Append(" files?\n");
                foreach (var item in items) msgBuilder.Append(" ").Append(item).Append('\n');
                if (!MessageBoxUtils.WarningYesNo(msgBuilder.ToString(), "Rename Files").IsYes()) return;

                /* Do rename */
                FileUtils.Rename(items, out var renameFailedList);

                /* Display failures */
                if (renameFailedList.Count > 0)
                {
                    msgBuilder.Clear().Append("Failed to rename following ").Append(renameFailedList.Count).Append(" files:\n");
                    foreach (var failedItem in renameFailedList) msgBuilder.Append(" ").Append(failedItem).Append('\n');
                    MessageBoxUtils.ErrorOk(msgBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBoxUtils.ErrorOk(ex.Message);
            }
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
            UpdateItemList();
        }

        private void UpdateItemList()
        {
            if (!IsLoaded) return;
            var searchingDirectory = DirectoryTextBox.Text;
            if (!Directory.Exists(searchingDirectory))
            {
                ItemsListBox.ItemsSource = new[] { "<Invalid Directory>" };
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
            ItemCountTextBlock.Text = $"({list.Count} items)";
            ItemsListBox.ItemsSource = list;
        }

        private void UpdateNewName()
        {
            if (ItemsListBox.SelectedItem is FileNode fileNode)
                NewNameTextBox.Text = _regex == null || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text)
                    ? fileNode.Name : _regex.Replace(fileNode.Name, RenamePatternTextBox.Text);
            if (ItemsListBox.SelectedItem is DirectoryNode directoryNode)
                NewNameTextBox.Text = _regex == null || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text)
                    ? directoryNode.Name : _regex.Replace(directoryNode.Name, RenamePatternTextBox.Text);
            else
                NewNameTextBox.Text = "";
            NewNameTextBox.Text = FileUtils.RemoveInvalidCharsForFileName(NewNameTextBox.Text);
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e) => DirectoryTextBox.Text = FileUtils.ExecutableDirectory;

        private void FilterPatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateFilter();

        private void RenamePatternTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateNewName();

        private void DirectoryTextBox_OnTextChanged(object sender, TextChangedEventArgs e) => UpdateItemList();

        private void CaseInsensitiveCheckBox_OnIsCheckedChanged(object sender, RoutedEventArgs e) => UpdateFilter();

        private void FileGroupingCheckBox_OnIsCheckedChanged(object sender, RoutedEventArgs e) => UpdateItemList();

        private void FilesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var showRenameGrid = !(ItemsListBox.SelectedItem is FileNode
                || (RenameDirectoriesCheckBox.IsChecked ?? false && ItemsListBox.SelectedItem is DirectoryNode));
            if (!showRenameGrid)
            {
                SingleItemRenameGrid.Visibility = Visibility.Collapsed;
                return;
            }
            SingleItemRenameGrid.Visibility = Visibility.Visible;
            UpdateNewName();
        }

        private void FilesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            switch (ItemsListBox.SelectedItem)
            {
                case DirectoryNode directoryNode:
                    DirectoryTextBox.Text = Path.Combine(directoryNode.Parent, directoryNode.Name);
                    break;
                case UpwardNode upwardNode:
                    DirectoryTextBox.Text = upwardNode.Parent;
                    break;
            }
        }

        private void RenameSelectedButton_OnClick(object sender, RoutedEventArgs e)
        {
            /* Preconditions */
            if (!(ItemsListBox.SelectedItem is Node node)) return; 
            if (!(ItemsListBox.SelectedItem is FileNode
                || (RenameDirectoriesCheckBox.IsChecked ?? false && ItemsListBox.SelectedItem is DirectoryNode))) return;
            var srcName = node.Name;
            var dstName = NewNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(dstName))
            {
                MessageBoxUtils.ErrorOk("New name cannot be empty!");
                return;
            }
            if (Equals(srcName, dstName)) return;

            /* List items to be renamed */
            var renamePairs = new LinkedList<FileUtils.RenamePair>();
            if (ItemsListBox.SelectedItem is FileNode fileNode)
                foreach (var suffix in fileNode.Suffixes)
                {
                    var srcPath = Path.Combine(node.Parent, srcName);
                    var dstPath = Path.Combine(node.Parent, dstName);
                    if (suffix != null)
                    {
                        srcPath += suffix;
                        dstPath += suffix;
                    }
                    renamePairs.AddLast(new FileUtils.RenamePair(srcPath, dstPath, false));
                }
            else if (ItemsListBox.SelectedItem is DirectoryNode)
                renamePairs.AddLast(new FileUtils.RenamePair(node.Parent, srcName, dstName, true));

            /* Do rename */
            Rename(renamePairs);
            UpdateItemList();
        }

        private void RenameAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            /* Preconditions */
            if (string.IsNullOrWhiteSpace(FilterPatternTextBox.Text) || string.IsNullOrWhiteSpace(RenamePatternTextBox.Text))
            {
                MessageBoxUtils.ErrorOk("Filter pattern and rename pattern cannot be empty!");
                return;
            }

            /* List items to be renamed */
            var renamePairs = new LinkedList<FileUtils.RenamePair>();
            if (RenameDirectoriesCheckBox.IsChecked ?? false) 
                foreach (var directoryNode in ItemsListBox.ItemsSource.OfType<DirectoryNode>())
                {
                    var dstName = _regex.Replace(directoryNode.Name, RenamePatternTextBox.Text);
                    if (Equals(directoryNode.Name, dstName)) continue;
                    renamePairs.AddLast(new FileUtils.RenamePair(directoryNode.Parent, directoryNode.Name, dstName, true));
                }
            foreach (var fileNode in ItemsListBox.ItemsSource.OfType<FileNode>())
            {
                var fileCount = fileNode.Suffixes.Count;
                if (fileCount <= 0) continue;
                var srcName = fileNode.Name;
                var dstName = _regex.Replace(srcName, RenamePatternTextBox.Text);
                if (Equals(srcName, dstName)) continue;
                foreach (var suffix in fileNode.Suffixes)
                {
                    var srcPath = Path.Combine(fileNode.Parent, srcName);
                    var dstPath = Path.Combine(fileNode.Parent, dstName);
                    if (suffix != null)
                    {
                        srcPath += suffix;
                        dstPath += suffix;
                    }
                    renamePairs.AddLast(new FileUtils.RenamePair(srcPath, dstPath, false));
                }
            }

            /* Do rename */
            Rename(renamePairs);
            UpdateItemList();
        }

    }
}
