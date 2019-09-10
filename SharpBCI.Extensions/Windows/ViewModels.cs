using SharpBCI.Core.Experiment;
using System;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Logging;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Windows
{

    public class ParamGroupHolder
    {

        public readonly ParamGroupHolder GroupHolder;

        public readonly ParameterGroup ParameterGroup;

        public readonly StackPanel GroupPanel, ItemsPanel;

        public readonly int Depth;

        private bool _collapsed = false, _visible = true;

        public ParamGroupHolder(ParamGroupHolder groupHolder, ParameterGroup parameterGroup, StackPanel groupPanel, int depth) 
            : this(groupHolder, parameterGroup, groupPanel, groupPanel, depth) { }

        public ParamGroupHolder(ParamGroupHolder groupHolder, ParameterGroup parameterGroup, StackPanel groupPanel, StackPanel itemsPanel, int depth)
        {
            GroupHolder = groupHolder;
            ParameterGroup = parameterGroup;
            GroupPanel = groupPanel;
            ItemsPanel = itemsPanel;
            Depth = depth;
        }

        public bool Collapsed
        {
            get => _collapsed;
            set
            {
                _collapsed = value;
                UpdateVisibility();
            }
        }

        public bool IsVisible
        {
            get => _visible;
            set
            {
                _visible = value;
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            GroupPanel.Visibility = _visible ? Visibility.Visible : Visibility.Collapsed;
            if (GroupPanel != ItemsPanel)
                ItemsPanel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        }

    }

    public class ParamHolder
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(ParamHolder));

        public readonly ParamGroupHolder GroupHolder;

        public readonly Grid RowContainer;

        public readonly TextBlock NameTextBlock;

        public readonly PresentedParameter PresentedParameter;

        public ParamHolder(ParamGroupHolder groupHolder, 
            Grid rowContainer, TextBlock nameTextBlock, PresentedParameter presentedParameter)
        {
            GroupHolder = groupHolder;
            RowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            NameTextBlock = nameTextBlock;
            PresentedParameter = presentedParameter ?? throw new ArgumentNullException(nameof(presentedParameter));
        }

        public bool IsVisible
        {
            get => RowContainer.Visibility == Visibility.Visible;
            set => RowContainer.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public IParameterDescriptor ParameterDescriptor => PresentedParameter.ParameterDescriptor;

        public PresentedParameter.ParamDelegates Delegates => PresentedParameter.Delegates;

        public bool CheckValid()
        {
            try
            {
                return CheckValid(PresentedParameter.Delegates.Getter());
            }
            catch (Exception e)
            {
                Logger.Warn("CheckValid", e, "parameter", ParameterDescriptor.Key);
                return false;
            }

        }

        public bool CheckValid(object value)
        {
            bool valid;
            try
            {
                valid = PresentedParameter.Delegates.Validator?.Invoke(value) ?? true;
            }
            catch (Exception e)
            {
                Logger.Warn("CheckValid", e, "parameter", ParameterDescriptor.Key, "value", value);
                valid = false;
            }
            Delegates.Updater?.Invoke(ParameterStateType.Valid, valid);
            return valid;
        }

    }

    public class SummaryHolder
    {

        public readonly ISummary Summary;

        public readonly Grid RowContainer;

        public readonly TextBlock ValueTextBlock;

        public SummaryHolder(ISummary summary, Grid rowContainer, TextBlock valueTextBlock)
        {
            Summary = summary;
            RowContainer = rowContainer;
            ValueTextBlock = valueTextBlock;
        }

        public bool IsVisible
        {
            get => RowContainer.Visibility == Visibility.Visible;
            set => RowContainer.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

    }

}
