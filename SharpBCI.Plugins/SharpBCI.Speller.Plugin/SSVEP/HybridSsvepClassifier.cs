using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Accord.Math;
using Accord.Statistics.Running;
using JetBrains.Annotations;
using MarukoLib.Interop;
using MarukoLib.Lang;
using MarukoLib.Logging;
using MarukoLib.Threading;
using MathNet.Numerics.IntegralTransforms;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;
using SharpBCI.Extensions.Patterns;
using Normal = MathNet.Numerics.Distributions.Normal;

namespace SharpBCI.Paradigms.Speller.SSVEP
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    internal sealed class HybridSsvepClassifier : AbstractSsvepClassifier
    {

        [StructLayout(LayoutKind.Sequential)]
        private struct CMat : IDisposable
        {

            public ulong id;
            public IntPtr ptr;
            public uint rows, cols;

            public void Dispose()
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ptr);
            }

        };

        [DllImport("CCA.dll", EntryPoint = "alloc_matrix", CallingConvention = CallingConvention.StdCall)]
        private static extern ulong AllocMatrix(CMat x);

        [DllImport("CCA.dll", EntryPoint = "delete_matrix", CallingConvention = CallingConvention.StdCall)]
        private static extern void DeleteMatrix(ulong id);

        [DllImport("CCA.dll", EntryPoint = "clear_matrices", CallingConvention = CallingConvention.StdCall)]
        private static extern void ClearMatrices();

        [DllImport("CCA.dll", EntryPoint = "compute_cca_qr", CallingConvention = CallingConvention.StdCall)]
        private static extern void ComputeCcaQr(ulong id);

        [DllImport("CCA.dll", EntryPoint = "canonical_correlation", CallingConvention = CallingConvention.StdCall)]
        private static extern double CanonicalCorrelation(CMat x, CMat y);

        [DllImport("CCA.dll", EntryPoint = "minimum_energy_combination", CallingConvention = CallingConvention.StdCall)]
        private static extern double MinimumEnergyCombination(CMat x, CMat y);

        internal class HarmonicGroup
        {

            public double Frequency;

            public ulong MatrixId;

            public double[] Data;

        }

        internal class Classifier
        {

            public Classifier(IFeatureExtractor featureExtractor, IPredictor predictor)
            {
                FeatureExtractor = featureExtractor;
                Predictor = predictor;
            }

            public IFeatureExtractor FeatureExtractor { get; }

            public IPredictor Predictor { get; }

        }

        internal interface IFeatureExtractor
        {

            double[] Process(double[] array);

        }

        internal interface IPredictor
        {

            int Predict(double[] features);

        }

        private class MaxScorePredictor : IPredictor
        {

            private readonly double _ccaThreshold;

            public MaxScorePredictor(double ccaThreshold) => _ccaThreshold = ccaThreshold;

            public int Predict(double[] ccaValues)
            {
                var maxScore = double.NegativeInfinity;
                var maxScoreIndex = -1;
                for (var i = 0; i < ccaValues.Length; i++)
                {
                    var score = ccaValues[i];
                    if (score >= _ccaThreshold && score > maxScore)
                    {
                        maxScore = score;
                        maxScoreIndex = i;
                    }
                }
                return maxScoreIndex;
            }

        }

        internal interface IInitializer : IStreamConsumer<Timestamped<ISample>>
        {

            void Accept(ISample sample);

            void Compute();

            void Initialize();

        }

        internal sealed class NoOpInitializer : StreamConsumer<Timestamped<ISample>>, IInitializer
        {

            public override void Accept(Timestamped<ISample> value) { }

            public void Accept(ISample sample) { }

            public void Compute() { }

            public void Initialize() { }

        }

        internal sealed class DistributionInitializer : StreamConsumer<Timestamped<ISample>>, IInitializer
        {

            private class StatisticsPredictor : IPredictor
            {

                private readonly Normal[] _distributions;

                public StatisticsPredictor(Normal[] distributions) => _distributions = distributions;

                public int Predict(double[] ccaValues)
                {
                    var minScore = double.PositiveInfinity;
                    var minScoreIndex = -1;
                    for (var i = 0; i < ccaValues.Length; i++)
                    {
                        var cca = ccaValues[i];
                        var normal = _distributions[i];
                        if (cca < normal.Mean) continue;
                        var score = normal.Density(cca);
                        if (score < minScore)
                        {
                            minScore = score;
                            minScoreIndex = i;
                        }
                    }
                    return minScoreIndex;
                }

            }

            private readonly LinkedList<ISample> _samples = new LinkedList<ISample>();

            private readonly HybridSsvepClassifier _hybridSsvepClassifier;

            private readonly RunningNormalStatistics[] _statisticsArray;

            public DistributionInitializer(HybridSsvepClassifier hybridSsvepClassifier)
            {
                _hybridSsvepClassifier = hybridSsvepClassifier;
                _statisticsArray = ArrayUtils.Initialize<RunningNormalStatistics>(_hybridSsvepClassifier._harmonicGroups.Length);
            }

            public void Accept(ISample sample) => _samples.AddLast(sample);

            public override void Accept(Timestamped<ISample> value) => Accept(value.Value);

            public void Compute()
            {
                if (_samples.IsEmpty()) return;
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var array = new double[(int)_hybridSsvepClassifier.WindowSize * _hybridSsvepClassifier.ChannelIndices.Length];
                foreach (var window in _samples
                    .Select(sample => sample[_hybridSsvepClassifier.ChannelIndices])
                    .MovingWindows(_hybridSsvepClassifier.WindowSize, 0.5))
                {
                    var offset = 0;
                    foreach (var sample in window)
                        foreach (var channel in sample)
                            array[offset++] = channel;
                    var features = _hybridSsvepClassifier.ComputeFeatures(array);
                    for (var i = 0; i < features.Length; i++)
                        _statisticsArray[i].Push(features[i]);
                }
                Logger.Info("Compute - baseline",
                    "means", $"[{_statisticsArray.Select(s => $"{s.Mean:F3}({s.StandardDeviation:F2})").Join(", ")}]",
                    "timeCost", stopwatch.ElapsedMilliseconds);
                _samples.Clear();
            }

            public void Initialize()
            {
                Compute();
                if (_statisticsArray[0].Count == 0) return;
                _hybridSsvepClassifier._predictor = new StatisticsPredictor(_statisticsArray.Select(stats => new Normal(stats.Mean, stats.StandardDeviation)).ToArray());
            }
        }

        private static readonly Logger Logger = Logger.GetLogger(typeof(HybridSsvepClassifier));

        private readonly ParallelPool _parallelPool;

        private readonly SpellerParadigm.Configuration.TestConfig.BandpassFilter[] _filterBank;

        private readonly SubBandMixingParams _subBandMixingParams;

        private readonly uint _harmonicsCount;

        private readonly HarmonicGroup[] _harmonicGroups;

        private IPredictor _predictor;

        public HybridSsvepClassifier([NotNull] IClock clock, uint parallel, [NotNull] CompositeTemporalPattern<SinusoidalPattern>[] patterns,
            SpellerParadigm.Configuration.TestConfig.BandpassFilter[] filterBank, 
            SubBandMixingParams subBandMixingParams,
            uint harmonicsCount, double ccaThreshold, [NotNull] uint[] channelIndices, double samplingRate, uint trialDurationMs, uint ssvepDelayMs)
            : base(clock, channelIndices, samplingRate, trialDurationMs, ssvepDelayMs)
        {
            if (patterns.Length == 0)
                throw new ArgumentException("at least one stimulation pattern is required");
            if (harmonicsCount <= 0)
                throw new ArgumentException("at least one harmonic is required");
            if (channelIndices.Length == 0)
                throw new ArgumentException("at least one channel is required");
            if (trialDurationMs <= 0)
                throw new ArgumentException("trial duration must be positive");
            _parallelPool = new ParallelPool(parallel);
            _filterBank = (SpellerParadigm.Configuration.TestConfig.BandpassFilter[])filterBank?.Empty2Null()?.Clone();
            _subBandMixingParams = subBandMixingParams;
            _harmonicsCount = harmonicsCount;
            _harmonicGroups = GenerateHarmonicGroups(samplingRate, WindowSize, patterns, harmonicsCount);
            _predictor = new MaxScorePredictor(ccaThreshold);
        }

        ~HybridSsvepClassifier()
        {
            if (_harmonicGroups != null)
                foreach (var harmonicGroup in _harmonicGroups)
                    DeleteMatrix(harmonicGroup.MatrixId);
        }

        public static void Test()
        {
            CMat ReadMatFromFile(string file)
            {
                IList<double> list = new List<double>();
                uint rows = 0;
                uint cols = 0;
                foreach (var line in File.ReadAllLines(file))
                {
                    var strValues = line.Split(',', ' ');
                    if (strValues.IsEmpty())
                        continue;
                    cols = (uint)strValues.Length;
                    foreach (var strVal in strValues)
                        list.Add(double.Parse(strVal));
                    rows++;
                }

                var hGroup = new HarmonicGroup
                {
                    Frequency = 0,
                    Data = list.ToArray()
                };
                var ptr = Marshal.AllocCoTaskMem(hGroup.Data.Length * Marshal.SizeOf(typeof(double)));
                Marshal.Copy(hGroup.Data, 0, ptr, hGroup.Data.Length);
                return new CMat
                {
                    ptr = ptr,
                    rows = rows,
                    cols = cols
                };
            }

            using (var x = ReadMatFromFile("E:/X.csv"))
            using (var y = ReadMatFromFile("E:/Y.csv"))
            {
                var allocatedX = new CMat { id = AllocMatrix(x) };
                var allocatedY = new CMat { id = AllocMatrix(y) };
                var stopwatch = new Stopwatch();

                stopwatch.Start();
                ComputeCcaQr(allocatedX.id);
                ComputeCcaQr(allocatedY.id);
                Logger.Info("Test - QR", "timeCost", stopwatch.ElapsedMilliseconds);

                stopwatch.Restart();
                var cor = CanonicalCorrelation(allocatedX, allocatedY);
                Logger.Info("Test - CCA", "cor", cor, "timeCost", stopwatch.ElapsedMilliseconds);

                DeleteMatrix(allocatedX.id);
                DeleteMatrix(allocatedY.id);
            }
        }

        private static Disposable<ulong> AllocateAndComputeQr(double[] array, uint rows, uint cols)
        {
            using (var coTaskArray = InteropArrays.Of(array))
                return AllocateAndComputeQr(coTaskArray, rows, cols);
        }

        private static Disposable<ulong> AllocateAndComputeQr(InteropArray<double> array, uint rows, uint cols)
        {
            var matId = AllocateMatrix(array, rows, cols);
            ComputeCcaQr(matId.Value);
            return matId;
        }

        private static Disposable<ulong> AllocateMatrix(double[] array, uint rows, uint cols)
        {
            using (var coTaskArray = InteropArrays.Of(array))
                return AllocateMatrix(coTaskArray, rows, cols);
        }

        private static Disposable<ulong> AllocateMatrix(InteropArray<double> array, uint rows, uint cols) =>
            new Disposable<ulong>.Delegated(AllocMatrix(new CMat
            {
                ptr = array.Ptr,
                rows = rows,
                cols = cols
            }), DeleteMatrix);

        private static HarmonicGroup[] GenerateHarmonicGroups(double samplingRate, uint windowSize,
            IReadOnlyList<CompositeTemporalPattern<SinusoidalPattern>> targetFlashingSchemes, uint harmonicsCount)
        {
            var output = new HarmonicGroup[targetFlashingSchemes.Count];
            var columnNum = harmonicsCount * 2;
            var harmonics = new double[columnNum * windowSize];
            using (var coTaskArray = InteropArrays.Of(harmonics))
                for (var f = 0; f < targetFlashingSchemes.Count; f++)
                {
                    var scheme = targetFlashingSchemes[f];
                    if (scheme.Patterns.Count != 1) throw new ArgumentException("can not support multi-pattern scheme");
                    var frequency = scheme.Patterns.First().Frequency; // TODO: to support multi-pattern scheme ??
                    var offset = 0;
                    for (var w = 0; w < windowSize; w++)
                    {
                        var t = w / samplingRate;
                        for (var i = 0; i < harmonicsCount; i++)
                        {
                            var frequencyMultiplier = i + 1.0;
                            var angle = frequency * t * 2 * Math.PI * frequencyMultiplier; // phase / frequencyMultiplier +
                            harmonics[offset++] = Math.Sin(angle);
                            harmonics[offset++] = Math.Cos(angle);
                        }
                    }
                    Marshal.Copy(harmonics, 0, coTaskArray.Ptr, harmonics.Length);
                    var matrixId = AllocMatrix(new CMat
                    {
                        ptr = coTaskArray.Ptr,
                        rows = windowSize,
                        cols = columnNum
                    });
                    ComputeCcaQr(matrixId);
                    output[f] = new HarmonicGroup { Frequency = frequency, MatrixId = matrixId };
                }
            return output;
        }

        public static Complex[] IdealBandpassFilter(double[] samples, int offset, int stride, int count,
            double frequency, double lowCutoff, double highCutoff,
            double[] outputSamples, int outputOffset, int outputStride,
            Complex[] reusableComplexArray)
        {
            if (count <= 0) return EmptyArray<Complex>.Instance;
            var complexArray = (reusableComplexArray == null || reusableComplexArray.Length != count) ? new Complex[count] : reusableComplexArray;
            for (var i = 0; i < count; i++)
                complexArray[i] = new Complex(samples[offset + i * stride], 0);
            Fourier.Forward(complexArray);
            var dF = frequency / count;
            for (var i = 0; i < count; i++)
            {
                var freq = Math.Abs(-frequency / 2 + i * dF);
                if (freq < lowCutoff || freq > highCutoff)
                    complexArray[InverseFourierShift(count, i)] = Complex.Zero;
            }
            Fourier.Inverse(complexArray);
            for (var i = 0; i < count; i++)
                outputSamples[outputOffset + i * outputStride] = complexArray[i].Real;
            return complexArray;
        }

        public static int InverseFourierShift(int count, int index)
        {
            var reminder = count % 2;
            var half = count / 2;
            return index < half ? half + index + reminder : index - half;
        }

        public override StreamConsumerPriority Priority => StreamConsumerPriority.Lowest;

        public IInitializer CreateInitializer() => new NoOpInitializer();//new DistributionInitializer(this);

        public int Predict(double[] frequencyFeatures) => _predictor.Predict(frequencyFeatures);

        /// <returns>-2 - timeout, -1 - no match</returns>
        public override int Classify()
        {
            if (!TryGetSamples(out var samples)) return -2;
            var array = new double[(int)WindowSize * ChannelIndices.Length];
            var offset = 0;
            foreach (var sample in samples)
                foreach (var channel in sample)
                {
                    array[offset++] = channel;
                    if (offset >= array.Length)
                        goto compute;
                }

            compute:
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var features = ComputeFeatures(array);
            Logger.Debug("Compute - cca", "features", $"[{features.Select(score => $"{score:F3}").Join(", ")}]", "timeCost", stopwatch.ElapsedMilliseconds);
            return Predict(features);
        }

        public static void ZScoreInPlace(double[] values)
        {
            var statistics = new RunningNormalStatistics();
            foreach (var value in values)
                statistics.Push(value);
            for (var i = 0; i < values.Length; i++)
                values[i] = (values[i] - statistics.Mean) / statistics.StandardDeviation;
        }

        public static void SoftmaxPlace(double[] values)
        {
            double sum = 0;
            for (var i = 0; i < values.Length; i++)
                values[i] = Math.Exp(values[i]);
            for (var i = 0; i < values.Length; i++)
                values[i] = values[i] / sum;
        }

        public double[] ComputeFeatures(double[] array)
        {
            var ccas = ComputeCanonicalCorrelations(array).Select(ArrayUtils.NaN2Zero).ToArray();
            Logger.Debug("ComputeFeatures - CCA", "values", $"[{ccas.Select(cca => $"{cca:F2}").Join(", ")}]");
            ZScoreInPlace(ccas);
            var mecs = ComputeMinimumEnergyCombinations(array).Select(ArrayUtils.NaN2Zero).ToArray();
            Logger.Debug("ComputeFeatures - MEC", "values", $"[{mecs.Select(mec => $"{mec:F2}").Join(", ")}]");
            ZScoreInPlace(mecs);
            var features = ccas.Select(ArrayUtils.NaN2Zero).ToArray().Add(mecs.Select(ArrayUtils.NaN2Zero).ToArray());
            //            SoftmaxPlace(features);
            return features;
        }

        public double[] ComputeCanonicalCorrelations(double[] array)
        {
            // Row: subject(window of signal) x Column: treatment(target frequency)
            var channelNum = ChannelIndices.Length;
            Debug.Assert(array.Length == WindowSize * channelNum);
            var ccaValues = new double[_harmonicGroups.Length];
            if (_filterBank == null)
            {
                using (var mat = AllocateAndComputeQr(array, WindowSize, (uint)channelNum))
                {
                    var x = new CMat { id = mat.Value };
                    _parallelPool.Batch(task =>
                    {
                        for (var h = task.TaskIndex; h < _harmonicGroups.Length; h += task.TotalTask)
                            ccaValues[h] = CanonicalCorrelation(x, new CMat { id = _harmonicGroups[h].MatrixId });
                    });
                }
            }
            else
            {
                var ccaMatrix = new double[_filterBank.Length, _harmonicGroups.Length];
                _parallelPool.Batch(task =>
                {
                    Complex[] complexArray = null;
                    var filteredSignal = new double[array.Length];
                    using (var coTaskArray = InteropArray<double>.Alloc(filteredSignal.Length))
                        for (var f = task.TaskIndex; f < _filterBank.Length; f += task.TotalTask)
                        {
                            var bandpassFilter = _filterBank[f];
                            // Filtering
                            for (var i = 0; i < channelNum; i++)
                            {
                                complexArray = IdealBandpassFilter(array, i, channelNum, (int)WindowSize,
                                    SamplingRate, bandpassFilter.LowCutOff, bandpassFilter.HighCutOff,
                                    filteredSignal, i, channelNum, complexArray);
                            }
                            Marshal.Copy(filteredSignal, 0, coTaskArray.Ptr, filteredSignal.Length);

                            // Calc
                            using (var xMatId = AllocateAndComputeQr(coTaskArray, WindowSize, (uint)channelNum))
                            {
                                var x = new CMat { id = xMatId.Value };
                                for (var h = 0; h < _harmonicGroups.Length; h++)
                                    ccaMatrix[f, h] = CanonicalCorrelation(x, new CMat { id = _harmonicGroups[h].MatrixId });
                            }
                        }
                });

                // Mixing sub-bands values.
                for (var h = 0; h < _harmonicGroups.Length; h++)
                    ccaValues[h] = _subBandMixingParams.MixSubBands(ccaMatrix.GetRow(h));
            }
            var sum = Enumerable.Sum(ccaValues);
            for (var i = 0; i < ccaValues.Length; i++)
                ccaValues[i] /= sum;
            return ccaValues;
        }

        public double[] ComputeMinimumEnergyCombinations(double[] array)
        {
            // Row: subject(window of signal) x Column: treatment(target frequency)
            var channelNum = ChannelIndices.Length;
            Debug.Assert(array.Length == WindowSize * channelNum);
            var mecValues = new double[_harmonicGroups.Length];
            using (var mat = AllocateMatrix(array, WindowSize, (uint)channelNum))
            {
                var x = new CMat { id = mat.Value };
                _parallelPool.Batch(task =>
                {
                    for (var h = task.TaskIndex; h < _harmonicGroups.Length; h += task.TotalTask)
                        mecValues[h] = MinimumEnergyCombination(x, new CMat { id = _harmonicGroups[h].MatrixId });
                });
            }
            var sum = Enumerable.Sum(mecValues);
            for (var i = 0; i < mecValues.Length; i++)
                mecValues[i] /= sum;
            return mecValues;
        }

    }
}
