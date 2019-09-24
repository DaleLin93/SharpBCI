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
                var context = new Context(value);
                if (_paramViewModels.Any())
                    using (_updateLock.Ref())
                        foreach (var entry in _paramViewModels)
                        {
                            if (context.TryGet(entry.Key, out var val))
                            {
                                if (!entry.Key.IsValid(val))
                                    throw new ArgumentException($"invalid parameter: {entry.Key}, value: {val}");
                            }
                            else
                                val = entry.Value.ParameterDescriptor.DefaultValue;
                            entry.Value.PresentedParameter.SetValue(val);
                        }
                _context = context;
                OnParamsUpdated();
            }
        }

        public void SetDescriptors(IParameterPresentAdapter adapter, IEnumerable<IDescriptor> descriptors)
        {
            Adapter = adapter;
            Descriptors = descriptors.ToArray();
            InitializeConfigurationPanel();
        }

        public void Refresh()
        {
            if (_updateLock.IsReferred) return;
            var context = new Context();
            foreach (var entry in _paramViewModels)
                try { context[entry.Key] = entry.Value.PresentedParameter.GetValue(); }
                catch (Exception) { _paramViewModels[entry.Key].PresentedParameter.SetValid(false); }
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
                        var presentedParameter = paramItem.GetPresenter().Present(paramItem, () => OnParamChanged(paramItem));
                        using (_updateLock.Ref()) // SetValue Default Value;
                            try { presentedParameter.SetValue(Context.TryGet(paramItem, out var val) ? val : paramItem.DefaultValue); } 
                            catch (Exception e) { throw new ProgrammingException($"Invalid default value: parameter {paramItem.Key}", e); }
                        var canReset = Adapter?.CanReset(paramItem) ?? false;
                        var nameTextBlock = ViewHelper.CreateParamNameTextBlock(paramItem, canReset);
                        if (canReset)
                            nameTextBlock.MouseLeftButtonDown += (s, e) =>
                            {
                                if (e.ClickCount != 2) return;
                                if (MessageBox.Show($"Set param '{paramItem.Name}' to default?", "Set to default", MessageBoxButton.YesNo,
                                        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                    presentedParameter.SetValue(paramItem.DefaultValue);
                                OnParamChanged(paramItem);
                            };
                        var rowGrid = groupMeta.Container.AddRow(nameTextBlock, presentedParameter.Element);
                        _paramViewModels[paramItem] = new ParamViewModel(groupMeta.Group, rowGrid, nameTextBlock, presentedParameter);
                        break;
                    case IGroupDescriptor groupItem:
                        if (_groupViewModels.ContainsKey(groupItem)) throw new UserException($"Invalid experiment, parameter group duplicated: {groupItem.Name}");
                        var depth = stack.Count - 1;
                        var canCollapse = Adapter?.CanCollapse(groupItem, depth) ?? false;
                        var groupViewModel = ViewHelper.CreateGroupViewModel(groupItem, depth, canCollapse);
                        groupMeta.Container.Children.Add(groupViewModel.GroupPanel);
                        stack.Push(new GroupMeta(groupViewModel, groupViewModel.ItemsPanel, groupViewModel.Group.Items.GetEnumerator()));
                        _groupViewModels[groupItem] = groupViewModel;
                        break;
                    default:
                        throw new UserException($"Unsupported group item: {currentItem.GetType().Name}");
                }
            } while (stack.Any());

            UpdateLayout();
            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
        }

        private void OnParamChanged(IParameterDescriptor parameter)
        {
            if (_updateLock.IsReferred) return;
            try { _context[parameter] = _paramViewModels[parameter].PresentedParameter.GetValue(); }
            catch (Exception) { /* ignored */ }
            OnParamsUpdated();
        }

        private void OnParamsUpdated()
        {
            if (_updateLock.IsReferred) return;
            if (GetInvalidParams().Any()) return;
            if (UpdateParamVisibility(_context))
                LayoutChanged?.Invoke(this, LayoutChangedEventArgs.NonInitialization);
            UpdateParamAvailability(_context);
            ContextChanged?.Invoke(this, new ContextChangedEventArgs(_context));
        }

        private bool UpdateParamVisibility(IReadonlyContext context)
        {
            var adapter = Adapter;
            if (adapter == null) return false;
            var visibilityChanged = false;
            foreach (var gViewModel in _groupViewModels.Values)
            {
                var visible = adapter.IsVisible(context, gViewModel.Group);
                if (visible != gViewModel.IsVisible)
                {
                    gViewModel.IsVisible = visible;
                    visibilityChanged = true;
                }
            }
            foreach (var pViewModel in _paramViewModels.Values)
            {
                var visible = adapter.IsVisible(context, pViewModel.ParameterDescriptor);
                if (visible != pViewModel.IsVisible)
                {
                    pViewModel.IsVisible = visible;
                    visibilityChanged = true;
                }
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
