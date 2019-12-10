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

        public void Update([CanBeNull] IReadonlyContext context, [CanBeNull] IParadigm paradigm)
        {
            context = context ?? EmptyContext.Instance;

            if (UpdateSummaryVisibility(context, false))
                LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
            foreach (var holder in _summaryViewModels.Where(sh => sh.IsVisible))
                try
                {
                    holder.ValueTextBlock.Text = holder.Summary.GetValue(context, paradigm).ToString();
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
                if (GroupHeader != null) Children.Add(ViewHelper.CreateGroupHeader(GroupHeader, null));
                foreach (var summary in Summaries)
                {
                    var valueTextBlock = new TextBlock { TextAlignment = TextAlignment.Right };
                    var summaryViewModel = new SummaryViewModel(summary, this.AddLabeledRow(summary.Name, valueTextBlock), valueTextBlock);
                    summaryViewModel.AnimationCompleted += (sender, e) => LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
                    _summaryViewModels.Add(summaryViewModel);
                }
            }

            UpdateSummaryVisibility(EmptyContext.Instance, true);
            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
        }

        private bool UpdateSummaryVisibility(IReadonlyContext context, bool initializing)
        {
            var adapter = Adapter;
            if (adapter == null) return false;
            var visibilityChanged = false;
            foreach (var summaryHolder in _summaryViewModels)
            {
                var visible = adapter.IsVisible(context, summaryHolder.Summary);
                if (visible != summaryHolder.IsVisible)
                {
                    summaryHolder.SetVisible(visible, !initializing);
                    visibilityChanged = true;
                }
            }
            return visibilityChanged;
        }

    }

}
