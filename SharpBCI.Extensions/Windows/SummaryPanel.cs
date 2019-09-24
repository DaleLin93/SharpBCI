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

        private readonly IList<SummaryViewModel> _summaryViewModels = new List<SummaryViewModel>();

        public string GroupHeader { get; set; } = "Summaries";

        [CanBeNull] public IReadOnlyCollection<ISummary> Summaries { get; private set; }

        [CanBeNull] public ISummaryPresentAdapter Adapter { get; private set; }

        public void SetSummaries(ISummaryPresentAdapter adapter, IEnumerable<ISummary> summaries)
        {
            Adapter = adapter;
            Summaries = summaries.ToArray();
            InitializeConfigurationPanel();
        }

        public void Update([CanBeNull] IReadonlyContext context, [CanBeNull] IExperiment experiment)
        {
            context = context ?? EmptyContext.Instance;

            if (UpdateSummaryVisibility(context))
                LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
            foreach (var holder in _summaryViewModels.Where(sh => sh.IsVisible))
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
            Children.Clear();
            _summaryViewModels.Clear();

            if (Summaries?.Any() ?? false)
            {
                var groupPanel = GroupHeader == null ? this : this.AddGroupPanel(GroupHeader, null);
                foreach (var summary in Summaries)
                {
                    var valueTextBlock = new TextBlock { TextAlignment = TextAlignment.Right };
                    _summaryViewModels.Add(new SummaryViewModel(summary, groupPanel.AddRow(summary.Name, valueTextBlock), valueTextBlock));
                }
            }

            UpdateLayout();
            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
        }

        private bool UpdateSummaryVisibility(IReadonlyContext context)
        {
            var adapter = Adapter;
            if (adapter == null) return false;
            var visibilityChanged = false;
            foreach (var summaryHolder in _summaryViewModels)
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
