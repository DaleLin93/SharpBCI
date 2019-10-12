﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using MarukoLib.DirectX;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpDX;
using SharpDX.Mathematics.Interop;
using DW = SharpDX.DirectWrite;
using D2D1 = SharpDX.Direct2D1;

namespace SharpBCI.Paradigms.VEP.MAVEP
{

    internal class MavepExperimentWindow : SimpleD2DForm
    {

        private readonly Session _session;

        private readonly MavepParadigm _paradigm;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        /* Paradigm variables */

        private bool _paradigmStarted = false, _trialStarted = false;

        private int _stimPos = 0;

        private string _displayText = null;

        private D2D1.Ellipse _fixationPoint;

        private readonly D2D1.Ellipse[] _stimulusPoints;

        /* Converted variables */

        private readonly Color _backgroundColor, _fontColor, _fixationColor;

        /* DirectX Resources */

        private DW.TextFormat _textFormat;

        public MavepExperimentWindow(Session session)
        {
            // ReSharper disable once LocalizableElement
            Text = "VEP";
            SuspendLayout();
            ControlBox = false;
            IsFullscreen = true;
            DoubleBuffered = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(true);

            Load += Window_OnLoaded;
            KeyUp += Window_OnKeyUp;
            this.HideCursorInside();

            _session = session;
            _paradigm = (MavepParadigm) session.Paradigm;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            _stageProgram = _paradigm.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            _stimulusPoints = new D2D1.Ellipse[_paradigm.Config.Gui.Stimulus.Count];

            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            _fontColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().Inverted().ToSdx();
            _fixationColor = _paradigm.Config.Gui.FixationPoint.Color.ToSdColor().ToSdx();

        }

        protected override void InitializeDirectXResources()
        {
            base.InitializeDirectXResources();
            _textFormat = new DW.TextFormat(DwFactory, "Arial", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, 84 * (float)GraphicsUtils.Scale)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            UpdateFixation();
        }

        protected override void ResizeRenderTarget()
        {
            base.ResizeRenderTarget();
            UpdateFixation();
        }

        protected override void DisposeDirectXResources()
        {
            _textFormat.Dispose();
            base.DisposeDirectXResources();
        }

        protected override void Draw(D2D1.RenderTarget renderTarget)
        {
            renderTarget.Clear(_backgroundColor);

            if (_paradigmStarted)
            {
                // Draw fixation
                SolidColorBrush.Color = _fixationColor;
                renderTarget.FillEllipse(_fixationPoint, SolidColorBrush);

                if (_trialStarted && _stimPos != 0)
                {
                    SolidColorBrush.Color = _fontColor;
                    foreach (var stimulusPoint in _stimulusPoints)
                        renderTarget.FillEllipse(stimulusPoint, SolidColorBrush);
                }
            }
            else if (!string.IsNullOrWhiteSpace(_displayText)) // Draw text
            {
                SolidColorBrush.Color = _fontColor;
                renderTarget.DrawText(_displayText, _textFormat, new RawRectangleF(0, 0, Width, Height),
                    SolidColorBrush, D2D1.DrawTextOptions.None);
            }
        }

        private void UpdateFixation()
        {
            var clientSize = ClientSize;
            var fixationSize = _paradigm.Config.Gui.FixationPoint.Size;
            _fixationPoint = new D2D1.Ellipse(new RawVector2(clientSize.Width / 2F, clientSize.Height / 2F), fixationSize, fixationSize);
        }

        private void GenerateStimuli()
        {
            var xSign = Math.Sign(_stimPos);
            var stimulusSize = (float)_paradigm.Config.Gui.Stimulus.Size;
            var tolerance = (float)_paradigm.Config.Gui.Stimulus.Tolerance;
            for (var i = 0; i < _stimulusPoints.Length; i++)
                _stimulusPoints[i] = new D2D1.Ellipse(new RawVector2
                {
                    X = _fixationPoint.Point.X + xSign * (float)_paradigm.Config.Gui.Stimulus.HorizontalOffset + _session.R.NextFloat(-tolerance, +tolerance),
                    Y = _fixationPoint.Point.Y + (float)_paradigm.Config.Gui.Stimulus.VerticalOffset + _session.R.NextFloat(-tolerance, +tolerance)
                }, stimulusSize, stimulusSize);
        }

        private void Window_OnLoaded(object sender, EventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            _markable?.Mark(MarkerDefinitions.UserExitMarker);
            Stop(true);
        }

        private void StageProgram_StageChanged(object sender, StageChangedEventArgs e)
        {
            /* Get next stage, exit on null (END REACHED) */
            if (e.IsEndReached)
            {
                this.ControlInvoke(self => Stop());
                return;
            }

            var stage = e.Stage;

            _displayText = stage.Cue;

            if (stage.Marker != null)
            {
                var marker = stage.Marker.Value;
                /* Record marker */
                _markable?.Mark(marker);

                switch (marker)
                {
                    case MarkerDefinitions.ParadigmStartMarker:
                        _paradigmStarted = true;
                        break;
                    case MarkerDefinitions.ParadigmEndMarker:
                        _paradigmStarted = false;
                        break;
                    case MarkerDefinitions.TrialStartMarker:
                        _trialStarted = true;
                        break;
                    case MarkerDefinitions.TrialEndMarker:
                        _trialStarted = false;
                        break;
                    case MavepParadigm.StimClearMarker:
                        _stimPos = 0;
                        break;
                    case MavepParadigm.LeftStimMarker:
                        _stimPos = -1;
                        GenerateStimuli();
                        break;
                    case MavepParadigm.RightStimMarker:
                        _stimPos = +1;
                        GenerateStimuli();
                        break;
                }
            }
        }

        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        private void Stop(bool userInterrupted = false)
        {
            Dispose();
            Close();
            _stageProgram.Stop();
            _session.Finish(userInterrupted);
        }

    }
    
}
