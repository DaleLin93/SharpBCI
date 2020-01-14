using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct CharSeqParams : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<CharSeqParams>
        {

            private static readonly Parameter<string> Sequence = new Parameter<string>("Sequence");

            private static readonly Parameter<bool> Shuffle = new Parameter<bool>("Shuffle", false);

            private static readonly Parameter<ushort> Repeat = new Parameter<ushort>("Repeat", 1);

            public override CharSeqParams Create(IParameterDescriptor parameter, IReadonlyContext context) => new CharSeqParams(Sequence.Get(context), Shuffle.Get(context), Repeat.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, CharSeqParams charSeqParams) => new Context
            {
                [Sequence] = charSeqParams.Sequence,
                [Shuffle] = charSeqParams.Shuffle,
                [Repeat] = charSeqParams.Repeat
            };

        }

        public readonly string Sequence;

        public readonly bool Shuffle;

        public readonly ushort Repeat;

        public CharSeqParams(string sequence, bool shuffle, ushort repeat)
        {
            Sequence = sequence;
            Shuffle = shuffle;
            Repeat = repeat;
        }

        public IEnumerable<char> GenerateSequence()
        {
            var seq = Shuffle ? (IEnumerable<char>) Sequence.ToCharArray() : Sequence;
            for (var i = 0; i < Repeat; i++)
            {
                if (Shuffle) ((char[])seq).Shuffle();
                // ReSharper disable once PossibleMultipleEnumeration
                foreach (var ch in seq)
                    yield return ch;
            }
        }

    }

}
