using System;
using System.Collections.Generic;
using SharpBCI.Core.IO;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Logging;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Experiments.Speller.P300
{

    internal sealed class P300Detector : Consumer<Timestamped<ISample>>
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(P300Detector));

        private readonly LinkedList<double[]> _samples = new LinkedList<double[]>();

        private readonly uint[] _channelIndices;

        private readonly double _samplingRate;

        private readonly uint _windowSize;

        private readonly double _overlap;

        private bool _actived = false;

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

        public bool Actived
        {
            get => _actived;
            set
            {
                if (value == _actived)
                    return;
                if (value)
                    _samples.Clear();
                _actived = value;
            }
        }

        public override ConsumerPriority Priority => ConsumerPriority.Lowest;

        public override void Accept(Timestamped<ISample> data)
        {
            if (Actived)
                _samples.AddLast(data.Value[_channelIndices]);
        }

        public int Compute()
        {
            return -1;
        }

    }

}
