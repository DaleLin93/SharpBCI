using System;
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

            private static readonly Parameter<double> Length = Parameter<double>.CreateBuilder("Length")
                .SetDefaultValue(0)
                .SetValidator(Predicates.Nonnegative)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, new GridLength(2.5, GridUnitType.Star))
                .Build();

            private static readonly Parameter<TimeUnit> Unit = Parameter<TimeUnit>.CreateBuilder("Unit")
                .SetDefaultValue(TimeUnit.Millisecond)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, ViewConstants.Star1GridLength)
                .Build();

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