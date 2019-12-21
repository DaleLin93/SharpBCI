using System;
using System.Collections.Generic;
using SharpBCI.Core.IO;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;

namespace SharpBCI.Paradigms.Speller.P300
{

    internal sealed class P300Detector : Core.IO.Consumer<Timestamped<ISample>>
    {

        private readonly LinkedList<double[]> _samples = new LinkedList<double[]>();

        private readonly uint[] _channelIndices;

        private readonly double _samplingRate;

        private readonly uint _windowSize;

        private readonly double _overlap;

        private bool _active;

        public P300Detector([NotNull] uint[] channelIndices, double samplingRate, uint windowSize, double overlap)
        {
            if (channelIndices.Length == 0)
                throw new ArgumentException("at least one channel is required");
            if (windowSize <= 0)
                throw new ArgumentException("window size must be positive");
            if (overlap < 0 || overlap >= 1)
                throw new ArgumentException("overlap must in range of [0, 1)");
            _channelIndices = (uint[])channelIndices.Clone();
            _samplingRate = samplingRate;
            _windowSize = windowSize;
            _overlap = overlap;
        }

        public bool IsActive
        {
            get => _active;
            set
            {
                if (value == _active)
                    return;
                if (value)
                    _samples.Clear();
                _active = value;
            }
        }

        public override Priority Priority => Priority.Lowest;

        public override void Accept(Timestamped<ISample> data)
        {
            if (IsActive)
                _samples.AddLast(data.Value[_channelIndices]);
        }

        public IdentificationResult Compute() => IdentificationResult.Missed;

    }

}
