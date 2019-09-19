using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Windows
{

    public class ParameterPanel : StackPanel
    {

        public event EventHandler<LayoutChangedEventArgs> LayoutChanged;

        public event EventHandler<ContextChangedEventArgs> ContextChanged;

        private readonly ReferenceCounter _updateLock = new ReferenceCounter();

        private readonly IDictionary<ParameterGroup, ParamGroupHolder> _paramGroupHolders =
            new Dictionary<ParameterGroup, ParamGroupHolder>();

        private readonly IDictionary<IParameterDescriptor, ParamHolder> _paramHolders =
            new Dictionary<IParameterDescriptor, ParamHolder>();

        private Context _context = new Context(64);

        private IDescriptor[] _descriptors;

        private bool _initialized = false;

        public ParameterPanel()
        {
            Loaded += (sender, args) =>
            {
                if (!_initialized) 
                    InitializeConfigurationPanel();
            };
        }

        [CanBeNull] public IDescriptor[] Descriptors
        {
            get => _descriptors;
            set
            {
                _descriptors = value;
                InitializeConfigurationPanel();
            }
        }

        [CanBeNull] public IParameterPresentAdapter Adapter { get; set; }

        [NotNull] public IReadonlyContext Context
        {
            get
            {
                var context = new Context();
                foreach (var entry in _paramHolders)
                    context[entry.Key] = _context.TryGet(entry.Key, out var val) 
                        ? val : entry.Value.ParameterDescriptor.DefaultValue;
                return context;
            }
            set
            {
                var context = new Context(value);
                if (_paramHolders.Any())
                    using (_updateLock.Ref())
                        foreach (var entry in _paramHolders)
                        {
                            if (context.TryGet(entry.Key, out var val))
                            {
                                if (!entry.Key.IsValid(val))
                                    throw new ArgumentException($"invalid parameter: {entry.Key}, value: {val}");
                            }
                            else
                                val = entry.Value.ParameterDescriptor.DefaultValue;
                            entry.Value.Delegates.Setter(val);
                        }
                _context = context;
                OnParamsUpdated();
            }
        }

        public void SetParamState(IParameterDescriptor parameter, ParameterStateType stateType, bool value) =>
            _paramHolders[parameter].Delegates.Updater?.Invoke(stateType, value);

        public void ResetToDefault() => Context = EmptyContext.Instance;

        public IEnumerable<IParameterDescriptor> GetInvalidParams() => from holder in _paramHolders.Values where !holder.CheckValid() select holder.ParameterDescriptor;

        private void InitializeConfigurationPanel()
        {
            var window = Window.GetWindow(this);

            if (window == null) return;

            Children.Clear();
            _paramGroupHolders.Clear();
            _paramHolders.Clear();

            var stack = new Stack<Tuple<ParamGroupHolder, StackPanel, IEnumerator<IDescriptor>>>();
            stack.Push(new Tuple<ParamGroupHolder, StackPanel, IEnumerator<IDescriptor>>(null, this,
                ((IEnumerable<IDescriptor>)Descriptors ?? EmptyArray<IDescriptor>.Instance).GetEnumerator()));
            do
            {
                var tuple = stack.Peek();
                if (!tuple.Item3.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                var currentItem = tuple.Item3.Current;
                switch (currentItem)
                {
                    case null:
                        continue;
                    case IParameterDescriptor paramItem:
                        if (_paramHolders.ContainsKey(paramItem)) throw new UserException($"Parameter duplicated: {paramItem.Key}");
                        var nameTextBlock = ViewHelper.CreateParamNameTextBlock(paramItem);
                        var presentedParameter = paramItem.GetPresenter().Present(paramItem, () => OnParamChanged(paramItem));
                        using (_updateLock.Ref()) // SetValue Default Value;
                            try { presentedParameter.Delegates.Setter(Context.TryGet(paramItem, out var val) ? val : paramItem.DefaultValue); } 
                            catch (Exception e) { throw new ProgrammingException($"Invalid default value: parameter {paramItem.Key}", e); }
                        nameTextBlock.MouseUp += (s, e) =>
                        {
                            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;
                            if (MessageBox.Show($"Set param '{paramItem.Name}' to default?", "Set to default", MessageBoxButton.YesNo,
                                    MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                presentedParameter.Delegates.Setter(paramItem.DefaultValue);
                        };
                        var rowContainer = tuple.Item2.AddRow(nameTextBlock, presentedParameter.Element);
                        _paramHolders[paramItem] = new ParamHolder(tuple.Item1, rowContainer, nameTextBlock, presentedParameter);
                        break;
                    case ParameterGroup groupItem:
                        if (_paramGroupHolders.ContainsKey(groupItem)) throw new UserException($"Invalid experiment, parameter group duplicated: {groupItem.Name}");
                        var depth = stack.Count - 1;
                        var groupPanel = tuple.Item2.AddGroupPanel(groupItem.Name, groupItem.Description, depth);
                        var paramsPanel = new StackPanel();
                        groupPanel.Children.Add(paramsPanel);
                        var groupHolder = new ParamGroupHolder(null, groupItem, groupPanel, paramsPanel, depth);
                        groupPanel.MouseLeftButtonUp += (sender, e) => groupHolder.Collapsed = !groupHolder.Collapsed;
                        stack.Push(new Tuple<ParamGroupHolder, StackPanel, IEnumerator<IDescriptor>>(groupHolder, groupHolder.GroupPanel, groupHolder.ParameterGroup.Items.GetEnumerator()));
                        _paramGroupHolders[groupItem] = groupHolder;
                        break;
                    default:
                        throw new UserException($"Unsupported group item: {currentItem.GetType().Name}");
                }
            } while (stack.Any());

            LayoutChanged?.Invoke(this, LayoutChangedEventArgs.Initialization);
            _initialized = true;
        }

        private void OnParamsChanged()
        {
            if (_updateLock.IsReferred) return;
            var context = new Context();
            foreach (var entry in _paramHolders)
                try { context[entry.Key] = entry.Value.Delegates.Getter(); }
                catch (Exception) { SetParamState(entry.Key, ParameterStateType.Valid, false); }
            _context = context;
            OnParamsUpdated();
        }

        private void OnParamChanged(IParameterDescriptor parameter)
        {
            if (_updateLock.IsReferred) return;
            try { _context[parameter] = _paramHolders[parameter].Delegates.Getter(); }
            catch (Exception) { SetParamState(parameter, ParameterStateType.Valid, false); }
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
            foreach (var groupHolder in _paramGroupHolders.Values)
            {
                var visible = adapter.IsVisible(context, groupHolder.ParameterGroup);
                if (visible != groupHolder.IsVisible)
                {
                    groupHolder.IsVisible = visible;
                    visibilityChanged = true;
                }
            }
            foreach (var paramHolder in _paramHolders.Values)
            {
                var visible = adapter.IsVisible(context, paramHolder.ParameterDescriptor);
                if (visible != paramHolder.IsVisible)
                {
                    paramHolder.IsVisible = visible;
                    visibilityChanged = true;
                }
            }
            return visibilityChanged;
        }

        private void UpdateParamAvailability(IReadonlyContext @params)
        {
            var adapter = Adapter;
            if (adapter == null) return;
            foreach (var paramHolder in _paramHolders.Values)
                paramHolder.Delegates.Updater?.Invoke(ParameterStateType.Enabled, adapter.IsEnabled(@params, paramHolder.ParameterDescriptor));
        }

    }

}
