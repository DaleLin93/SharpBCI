using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Windows
{
    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ResultWindow.xaml
    /// </summary>
    internal partial class ResultWindow
    {

        private readonly LinkedList<Result.Item> _allDisplayItems = new LinkedList<Result.Item>();

        private readonly Session _session;

        public ResultWindow(Session session)
        {
            InitializeComponent();

            _session = session;

            SessionNameTextBlock.Text = session.Subject + " - " + session.Descriptor;

            var displayDataList = session.Result?.Items ?? new LinkedList<Result.Item>();
            
            foreach (var data in displayDataList)
                _allDisplayItems.AddLast(data);

            if (_allDisplayItems.Count > 0)
                _allDisplayItems.AddLast(Result.Item.Separator);
            _allDisplayItems.AddLast(new Result.Item("Data File", session.DataFilePrefix + ".*"));

            InitUI();
        }

        // ReSharper disable once InconsistentNaming
        public void InitUI()
        {
            foreach (var item in _allDisplayItems)
            {
                if (Result.Item.IsSeparator(item)) continue;
                var grid = new Grid();
                var titleTextBlock = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    TextWrapping = TextWrapping.NoWrap,
                    Text = item.Title + ":",
                };
                var valueTextBlock = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness { Left = 150 },
                    TextWrapping = TextWrapping.NoWrap,
                    Text = item.Value,
                };
                grid.Children.Add(titleTextBlock);
                grid.Children.Add(valueTextBlock);
                SessionPanel.Children.Add(grid);
            }
            UpdateLayout();
        }

        private void SessionNameTextBlock_OnMouseUp(object sender, MouseButtonEventArgs e) => Process.Start(Path.GetFullPath(App.DataDir));

        private void SaveSnapshotBtn_OnClick(object sender, RoutedEventArgs e)
        {
            SessionPanel.RenderImage((int)SessionPanel.ActualWidth, (int)SessionPanel.ActualHeight).WritePng(_session.GetDataFileName(".result.png"));
            SaveSnapshotBtn.IsEnabled = false;
        }

    }
}
