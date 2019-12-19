using System;
using System.Collections.Generic;
using System.Linq;
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

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter)
            {
                var parameters = new IParameterDescriptor[] {Length, Unit};
                if (parameter.Metadata.Contains(IncludedTimeUnitsProperty) || parameter.Metadata.Contains(ExcludedTimeUnitsProperty))
                {
                    var meta = new Context(Unit.Metadata);
                    SelectablePresenter.SelectableValuesFuncProperty.Set(meta, p =>
                    {
                        var list = new LinkedList<TimeUnit>();
                        list.AddAll(IncludedTimeUnitsProperty.TryGet(p.Metadata, out var included) ? included : Enum.GetValues(typeof(TimeUnit)).Cast<TimeUnit>());
                        if (ExcludedTimeUnitsProperty.TryGet(p.Metadata, out var excluded)) list.RemoveAll(excluded);
                        return list.ToArray();
                    });
                    parameters[1] = new MetadataOverridenParameter(Unit, meta);
                }
                return parameters;
            }

            public override TimeInterval Create(IParameterDescriptor parameter, IReadonlyContext context) => new TimeInterval(Length.Get(context), Unit.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, TimeInterval timeInterval) => new Context
            {
                [Length] = timeInterval.Length,
                [Unit] = timeInterval.Unit
            };

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