using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using MarukoLib.DirectX;
using SharpBCI.Core.IO;
using MarukoLib.Lang;
using SharpDX.Mathematics.Interop;
using MarukoLib.Logging;

namespace SharpBCI.Experiments.Speller.EyeTracking
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    internal sealed class EyeTrackingDetector : TransformedConsumer<Timestamped<Point>, Point>
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(EyeTrackingDetector));
        
        private readonly object _lock = new object();

        private readonly LinkedList<Point> _points = new LinkedList<Point>();

        private RawRectangleF _container;

        private bool _actived = false;

        public EyeTrackingDetector() : base(t => t.Value) { }

        public override ConsumerPriority Priority => ConsumerPriority.Lowest;

        public void Active(RawRectangleF container)
        {
            lock (_lock)
            {
                _container = container;
                _actived = true;
            }
        }

        public int GetResult(SpellerBaseWindow.UIButton[] buttons)
        {
            lock (_lock)
            {
                var count = _points.Count;
                if (count <= 0) return -1;
                double xSum = 0, ySum = 0;
                foreach (var point in _points)
                {
                    xSum += point.X;
                    ySum += point.Y;
                }
                double x = xSum / count, y = ySum / count;
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (button == null) continue;
                    if (button.BorderRect.Contains(x, y)) return i;
                }
                return -1;
            }
//                return _points.All(p => _container.Contains(p.X, p.Y));
        }

        public void Reset()
        {
            lock (_lock)
            {
                _actived = false;
                _points.Clear();
            }
        }

        public override void Accept(Point value)
        {
            lock (_lock)
                if (_actived)
                    _points.AddLast(value);
        }

    }

}
