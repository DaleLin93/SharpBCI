using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Windows
{

    public class ParameterPanel : StackPanel
    {

        private struct GroupMeta
        {

            internal readonly GroupViewModel Group;

            internal readonly StackPanel Container;

            internal readonly IEnumerator<IDescriptor> Items;

            public GroupMeta(GroupViewModel group, StackPanel container, IEnumerator<IDescriptor> items)
            {
                Group = group;
                Container = container;
                Items = items;
            }

        }

        public event EventHandler<LayoutChangedEventArgs> LayoutChanged;

        public event EventHandler<ContextChangedEventArgs> ContextChanged;

        private readonly ReferenceCounter _updateLock = new ReferenceCounter();

        private readonly IDictionary<IGroupDescriptor, GroupViewModel> _groupViewModels =
            new Dictionary<IGroupDescriptor, GroupViewModel>();

        private readonly IDictionary<IParameterDescriptor, ParamViewModel> _paramViewModels =
            new Dictionary<IParameterDescriptor, ParamViewModel>();

        private Context _context = new Context(64);

        public ParameterPanel() => VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);

        [CanBeNull] public GroupViewModel this[IGroupDescriptor group] => _groupViewModels.TryGetValue(group, out var viewModel) ? viewModel : null;

        [CanBeNull] public ParamViewModel this[IParameterDescriptor parameter] => _paramViewModels.TryGetValue(parameter, out var viewModel) ? viewModel : null;

        public bool CanCollapse { get; set; } = true;

        [CanBeNull] public IParameterPresentAdapter Adapter { get; private set; }

        [CanBeNull] public IReadOnlyCollection<IDescriptor> Descriptors { get; private set; }

        [NotNull] public IReadonlyContext Context
        {
            get
            {
                var context = new Context();
                foreach (var entry in _paramViewModels)
                    context[entry.Key] = _context.TryGet(entry.Key, out var val) 
                        ? val : entry.Value.ParameterDescriptor.DefaultValue;
                return context;
            }
            set
            {
                if (_paramViewModels.Any())
                    using (_updateLock.Ref())
                        foreach (var entry in _paramViewModels)
                        {
                            if (!value.TryGet(entry.Key, out var val) || !entry.Key.IsValid(val))
                                val = entry.Value.ParameterDescriptor.DefaultValue;
                            entry.Value.PresentedParameter.SetValue(val);
                        }
                Refresh();
            }
        }

        public void SetDescriptors(IParameterPresentAdapter adapter, IEnumerable<IDescriptor> descriptors)
        {
            Adapter = adapter;
            Descriptors = descriptors?.ToArray() ?? EmptyArray<IDescriptor>.Instance;
            InitializeConfigurationPanel();
        }

        public void Refresh()
        {
            var context = new Context();
            foreach (var entry in _paramViewModels)
                try { context[entry.Key] = entry.Value.PresentedParameter.GetValue(); }
                catch (Exception) { /* ignored */ }
            _context = context;
            OnParamsUpdated();
        }

        public void ResetToDefault() => Context = EmptyContext.Instance;

        public IEnumerable<IParameterDescriptor> GetInvalidParams() => 
            from pvm in _paramViewModels.Values
            where !pvm.PresentedParameter.IsValid
            select pvm.ParameterDescriptor;

        private void InitializeConfigurationPanel()
        {
            Children.Clear();
            _groupViewModels.Clear();
            _paramViewModels.Clear();

            var paramKeySet = new HashSet<string>();
            var stack = new Stack<GroupMeta>();
            stack.Push(new GroupMeta(null, this, (Descriptors ?? EmptyArray<IDescriptor>.Instance).GetEnumerator()));
            do
            {
                var groupMeta = stack.Peek();
                if (!groupMeta.Items.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                var currentItem = groupMeta.Items.Current;
                switch (currentItem)
                {
                    case null:
                        continue;
                    case IParameterDescriptor paramItem:
                        if (_paramViewModels.ContainsKey(paramItem)) throw new UserException($"Parameter duplicated: {paramItem.Key}");
                        if (!paramKeySet.Add(paramItem.Key)) throw new UserException($"Parameter key duplicated: {paramItem.Key}");
                        var presentedParameter = paramItem.GetPresenter().Present(paramItem, () => OnParamChanged(paramItem));
                        using (_updateLock.Ref()) // SetValue Default Value;
                            try { presentedParameter.SetValue(_context.TryGet(paramItem, out var val) ? val : paramItem.DefaultValue); } 
                            catch (Exception e) { throw new ProgrammingException($"Invalid default value: parameter {paramItem.Key}", e); }
                        var canReset = Adapter?.CanReset(paramItem) ?? false;
                        var nameTextBlock = ViewHelper.CreateParamNameTextBlock(paramItem, canReset);
                        if (canReset)
                            nameTextBlock.MouseLeftButtonDown += (s, e) =>
                            {
                                if (e.ClickCount != 2) return;
                                if (MessageBox.Show($"Set param '{paramItem.Name}' to default?", "Set to default", MessageBoxButton.YesNo,
                                        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                    using (_updateLock.Ref())
                                        presentedParameter.SetValue(paramItem.DefaultValue);
                                OnParamChanged(paramItem);
                            };
                        var rowGrid = groupMeta.Container.AddLabeledRow(nameTextBlock, presentedParameter.Element);
                        var paramViewModel = new ParamViewModel(groupMeta.Group, rowGrid, nameTextBlock, presentedParameter);
                        paramViewModel.AnimationCompleted += (sender, e) => LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
                        _paramViewModels[paramItem] = paramViewModel;
                        break;
                    case IGroupDescriptor groupItem:
                        if (_groupViewModels.ContainsKey(groupItem)) throw new UserException($"Invalid paradigm, parameter group duplicated: {groupItem.Name}");
                        var depth = stack.Count - 1;
                        var canCollapse = Adapter?.CanCollapse(groupItem, depth) ?? false;
                        var groupViewModel = ViewHelper.CreateGroupViewModel(groupItem, depth, canCollapse, () => CanCollapse);
                        groupViewModel.AnimationCompleted += (sender, e) => LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
                        groupMeta.Container.Children.Add(groupViewModel.GroupPanel);
                        stack.Push(new GroupMeta(groupViewModel, groupViewModel.ItemsPanel, groupViewModel.Group.Items.GetEnumerator()));
                        _groupViewModels[groupItem] = groupViewModel;
                        break;
                    default:
                        throw new UserException($"Unsupported group item: {currentItem.GetType().Name}");
                }
            } while (stack.Any());

            OnParamsUpdated(true);
            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
        }

        private void OnParamChanged(IParameterDescriptor parameter)
        {
            if (_updateLock.IsReferred) return;
            try { _context[parameter] = _paramViewModels[parameter].PresentedParameter.GetValue(); }
            catch (Exception) { /* ignored */ }
            OnParamsUpdated();
        }

        private void OnParamsUpdated(bool initializing = false) 
        {
            if (_updateLock.IsReferred) return;
            if (GetInvalidParams().Any()) return;
            UpdateParamVisibility(_context, initializing);
            UpdateParamAvailability(_context);
            ContextChanged?.Invoke(this, new ContextChangedEventArgs(_context));
        }

        private bool UpdateParamVisibility(IReadonlyContext context, bool initializing)
        {
            var adapter = Adapter;
            if (adapter == null) return false;
            var visibilityChanged = false;
            foreach (var pViewModel in _paramViewModels.Values)
            {
                var visible = adapter.IsVisible(context, pViewModel.ParameterDescriptor);
                if (visible == pViewModel.IsVisible) continue;
                pViewModel.SetVisible(visible, (pViewModel.Group?.IsVisible ?? true) && !initializing);
                visibilityChanged = true;
            }
            foreach (var gViewModel in _groupViewModels.Values)
            {
                var visible = adapter.IsVisible(context, gViewModel.Group);
                if (visible == gViewModel.IsVisible) continue;
                gViewModel.SetVisible(visible, !initializing);
                visibilityChanged = true;
            }
            return visibilityChanged;
        }

        private void UpdateParamAvailability(IReadonlyContext @params)
        {
            var adapter = Adapter;
            if (adapter == null) return;
            foreach (var paramHolder in _paramViewModels.Values)
                paramHolder.PresentedParameter.SetEnabled(adapter.IsEnabled(@params, paramHolder.ParameterDescriptor));
        }

    }

}
