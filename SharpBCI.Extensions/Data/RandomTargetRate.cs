using System;
using MarukoLib.Lang;
using MarukoLib.Lang.Sequence;

namespace SharpBCI.Extensions.Data
{
    [ParameterizedObject(typeof(Factory))]
    public struct RandomTargetRate : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<RandomTargetRate>
        {

            private static readonly Parameter<bool> Pseudo = new Parameter<bool>("Pseudo", true);

            private static readonly Parameter<decimal> TargetRate = new Parameter<decimal>("Probability", "%", null, value => value >= 0 && value <= 100, 0);

            public override RandomTargetRate Create(IParameterDescriptor parameter, IReadonlyContext context) => new RandomTargetRate(Pseudo.Get(context), TargetRate.Get(context) / 100);

            public override IReadonlyContext Parse(IParameterDescriptor parameter, RandomTargetRate randomTargetRate) => new Context
            {
                [Pseudo] = (randomTargetRate).Pseudo,
                [TargetRate] = (uint) Math.Round((randomTargetRate).Probability * 100),
            };

        }

        public readonly bool Pseudo;

        public readonly decimal Probability;

        public RandomTargetRate(bool pseudo, decimal probability)
        {
            Pseudo = pseudo;
            Probability = probability;
        }

        public IRandomBools CreateRandomBoolSequence() => CreateRandomBoolSequence((int) DateTimeUtils.CurrentTimeTicks);

        public IRandomBools CreateRandomBoolSequence(int seed) => Pseudo ? (IRandomBools) new PseudoRandomBools(Probability, seed) : new RandomBools(seed, Probability);

    }
}