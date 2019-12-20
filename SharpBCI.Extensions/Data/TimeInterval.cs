using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using MarukoLib.Lang;
using SharpBCI.Extensions.Presenters;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct TimeInterval : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<TimeInterval>
        {

            public static readonly NamedProperty<TimeUnit[]> IncludedTimeUnitsProperty = new NamedProperty<TimeUnit[]>("IncludedTimeUnits");

            public static readonly NamedProperty<TimeUnit[]> ExcludedTimeUnitsProperty = new NamedProperty<TimeUnit[]>("ExcludedTimeUnits");

            private static readonly Parameter<double> Length = Parameter<double>.CreateBuilder("Length")
                .SetDefaultValue(0)
                .SetValidator(Predicates.Nonnegative)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, new GridLength(2.5, GridUnitType.Star))
                .Build();

            private static readonly Parameter<TimeUnit> Unit = Parameter<TimeUnit>.CreateBuilder("Unit")
                .SetSelectablesForEnum()
                .SetDefaultValue(TimeUnit.Millisecond)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, ViewConstants.Star1GridLength)
                .Build();

            private static readonly IParameterDescriptor[] DefaultParameters = {Length, Unit};

            private readonly ConditionalWeakTable<IParameterDescriptor, IParameterDescriptor[]> _cache;

            public Factory() => _cache = new ConditionalWeakTable<IParameterDescriptor, IParameterDescriptor[]>();

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => GetParameterArray(parameter);

            public override TimeInterval Create(IParameterDescriptor parameter, IReadonlyContext context) => 
                new TimeInterval(Length.Get(context), context.GetOrDefault(GetParameterArray(parameter)[1], Unit.DefaultValue));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, TimeInterval timeInterval) => new Context
            {
                [Length] = timeInterval.Length,
                [GetParameterArray(parameter)[1]] = timeInterval.Unit
            };

            private IParameterDescriptor[] GetParameterArray(IParameterDescriptor parameter)
            {
                if (!parameter.Metadata.Contains(IncludedTimeUnitsProperty) && !parameter.Metadata.Contains(ExcludedTimeUnitsProperty))
                    return DefaultParameters;
                if (_cache.TryGetValue(parameter, out var cachedParameters)) return cachedParameters;
                var meta = new Context(Unit.Metadata);
                SelectablePresenter.SelectableValuesFuncProperty.Set(meta, p =>
                {
                    var list = new LinkedList<TimeUnit>();
                    list.AddAll(IncludedTimeUnitsProperty.TryGet(parameter.Metadata, out var included) ? included : Enum.GetValues(typeof(TimeUnit)).Cast<TimeUnit>());
                    if (ExcludedTimeUnitsProperty.TryGet(parameter.Metadata, out var excluded)) list.RemoveAll(excluded);
                    return list.ToArray();
                });
                var parameters = new IParameterDescriptor[] { Length, new MetadataOverridenParameter(Unit, meta) };
                _cache.Add(parameter, parameters);
                return parameters;
            }

        }

        public readonly double Length;

        public readonly TimeUnit Unit;

        public TimeInterval(double length, TimeUnit unit)
        {
            Length = length;
            Unit = unit;
        }

        public TimeSpan TimeSpan => Unit.ToTimeSpan(Length);

    }

}