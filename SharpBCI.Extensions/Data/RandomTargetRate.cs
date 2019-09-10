using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{
    [ParameterizedObject(typeof(Factory))]
    public struct RandomTargetRate : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<RandomTargetRate>
        {

            private static readonly Parameter<bool> Pseudo = new Parameter<bool>("Pseudo", true);

            private static readonly Parameter<uint> TargetRate = new Parameter<uint>("Probability", unit:"%", null, 0);

            public override RandomTargetRate Create(IParameterDescriptor parameter, IReadonlyContext context) => new RandomTargetRate(Pseudo.Get(context), TargetRate.Get(context) / 100.0F);

            public override IReadonlyContext Parse(IParameterDescriptor parameter, RandomTargetRate randomTargetRate) => new Context
            {
                [Pseudo] = (randomTargetRate).Pseudo,
                [TargetRate] = (uint) Math.Round((randomTargetRate).Probability * 100),
            };

        }

        public readonly bool Pseudo;

        public readonly float Probability;

        public RandomTargetRate(bool pseudo, float probability)
        {
            Pseudo = pseudo;
            Probability = probability;
        }

        public IRandomBoolSequence CreateRandomBoolSequence() => CreateRandomBoolSequence((int) DateTimeUtils.CurrentTimeTicks);

        public IRandomBoolSequence CreateRandomBoolSequence(int seed) => Pseudo ? (IRandomBoolSequence)new PseudoRandom(Probability, seed) : new RandomBools(seed, Probability);

    }
}