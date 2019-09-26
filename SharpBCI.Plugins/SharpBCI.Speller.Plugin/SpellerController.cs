using System;

namespace SharpBCI.Paradigms.Speller
{

    internal class SpellerController
    {

        public event EventHandler Calibrated;

        public event EventHandler Starting;

        public event EventHandler Stopping;

        public event EventHandler CreatingTrial;

        public event EventHandler CancellingTrial;

        public void CalibrationComplete() => Calibrated?.Invoke(this, EventArgs.Empty);

        public void Start() => Starting?.Invoke(this, EventArgs.Empty);

        public void Stop() => Stopping?.Invoke(this, EventArgs.Empty);

        public void CreateTrial() => CreatingTrial?.Invoke(this, EventArgs.Empty);

        public void CancelTrial() => CancellingTrial?.Invoke(this, EventArgs.Empty);

    }

}
