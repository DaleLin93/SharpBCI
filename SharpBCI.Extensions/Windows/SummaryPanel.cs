using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Logging;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Extensions.Windows
{

    public class SummaryPanel : StackPanel
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(SummaryPanel));

        public event EventHandler<LayoutChangedEventArgs> LayoutChanged;

        private readonly IList<SummaryHolder> _summaryHolders = new List<SummaryHolder>();

        private ISummary[] _summaries;

        private bool _initialized = false;

        public SummaryPanel()
        {
            Loaded += (sender, args) =>
            {
                if (!_initialized) 
                    InitializeConfigurationPanel();
            };
        }

        public string GroupHeader { get; set; } = "Summaries";

        [CanBeNull] public ISummary[] Summaries
        {
            get => _summaries;
            set
            {
                _summaries = value;
                InitializeConfigurationPanel();
            }
        }

        [CanBeNull] public ISummaryPresentAdapter Adapter { get; set; }

        public void Update([CanBeNull] IReadonlyContext context, [CanBeNull] IExperiment experiment)
        {
            context = context ?? EmptyContext.Instance;

            if (UpdateSummaryVisibility(context))
                LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
            foreach (var holder in _summaryHolders.Where(sh => sh.IsVisible))
                try
                {
                    holder.ValueTextBlock.Text = holder.Summary.GetValue(context, experiment).ToString();
                    holder.ValueTextBlock.Foreground = SystemColors.WindowTextBrush;
                }
                catch (Exception e)
                {
                    Logger.Warn("Update - summary", e, "summary", holder.Summary.Name);
                    holder.ValueTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    holder.ValueTextBlock.Text = "Err";
                }
        }

        private void InitializeConfigurationPanel()
        {
            var window = Window.GetWindow(this);

            if (window == null) return;

            Children.Clear();
            _summaryHolders.Clear();

            if (_summaries.Any())
            {
                var groupPanel = GroupHeader == null ? this : this.AddGroupPanel(GroupHeader, null);
                foreach (var summary in _summaries)
                {
                    var valueTextBlock = new TextBlock { TextAlignment = TextAlignment.Right };
                    _summaryHolders.Add(new SummaryHolder(summary, groupPanel.AddRow(summary.Name, valueTextBlock), valueTextBlock));
                }
            }

            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
            _initialized = true;
        }

        private bool UpdateSummaryVisibility(IReadonlyContext context)
        {
            var adapter = Adapter;
            if (adapter == null) return false;
            var visibilityChanged = false;
            foreach (var summaryHolder in _summaryHolders)
            {
                var visible = adapter.IsVisible(context, summaryHolder.Summary);
                if (visible != summaryHolder.IsVisible)
                {
                    summaryHolder.IsVisible = visible;
                    visibilityChanged = true;
                }
            }
            return visibilityChanged;
        }

    }

}
