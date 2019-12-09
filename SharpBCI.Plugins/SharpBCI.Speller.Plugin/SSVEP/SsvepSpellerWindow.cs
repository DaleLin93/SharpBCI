using SharpBCI.Core.Staging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using MarukoLib.Lang;
using MarukoLib.Persistence;
using SharpBCI.Core.Experiment;
using DW = SharpDX.DirectWrite;
using D2D1 = SharpDX.Direct2D1;
using SharpDX;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Patterns;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;

namespace SharpBCI.Paradigms.Speller.SSVEP
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    internal class SsvepSpellerWindow : SpellerExperimentBaseWindow
    {

        private class SsvepResult : SpellerParadigm.Result
        {

            public const string FrequenciesFile = ".frequencies";

            public const string TrialsFile = ".trials";

            public override void Save(Session session)
            {
                base.Save(session);
                ((SpellerParadigm) session.Paradigm).Config.Test.StimulationPatterns?
                    .JsonSerializeToFile(session.GetDataFileName(FrequenciesFile), JsonUtils.PrettyFormat, Encoding.UTF8);
                Trials?.JsonSerializeToFile(session.GetDataFileName(TrialsFile), JsonUtils.PrettyFormat, Encoding.UTF8);
            }

        }

        public class SsvepButton : SpellerParadigm.Result.Button
        {

            public int FrequencyIndex;

            public SsvepButton(KeyDescriptor keyDescriptor, int frequencyIndex) : base(keyDescriptor) =>
                FrequencyIndex = frequencyIndex;

        }

        private class SsvepTrial : SpellerParadigm.Result.Trial
        {

            public int? TargetFrequencyIndex;

            public int? SelectedFrequencyIndex;

        }

        private readonly BiosignalStreamer _biosignalStreamer;

        private readonly HybridSsvepClassifier _hybridSsvepClassifier;

        private readonly CompositeTemporalPattern<SinusoidalPattern>[] _stimulationPatterns;

        /* Paradigm variables */

        private volatile UIButton[] _activedButtons;

        private HybridSsvepClassifier.IInitializer _initializer;

        private SsvepTrial _trial;

        /* D3D Resources */

        private DW.TextFormat _frequencyTextFormat;

        private D2D1.RadialGradientBrush _radialGradientBrush;

        public SsvepSpellerWindow(Session session, SpellerController spellerController) : base(session, spellerController)
        {
            SuspendLayout();
            ControlBox = false;
            IsFullscreen = true;
            DoubleBuffered = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(true);

            SpellerController.CancellingTrial += (sender, e) =>
            {
                if (_trial != null)
                {
                    StageProgram.Skip();
                    TrialCancelled = true;
                }
            };

            _stimulationPatterns = Paradigm.Config.Test.StimulationPatterns;

            if (session.StreamerCollection.TryFindFirst(out _biosignalStreamer))
            {
                _hybridSsvepClassifier = new HybridSsvepClassifier(session.Clock, Paradigm.Config.Test.ComputeParallelLevel, 
                    _stimulationPatterns, Paradigm.Config.Test.FilterBank, Paradigm.Config.Test.SubBandMixingParams,
                    Paradigm.Config.Test.HarmonicsCount, Paradigm.Config.Test.CcaThreshold,
                    Paradigm.Config.Test.Channels.Enumerate(1, _biosignalStreamer.BiosignalSource.ChannelNum).Select(i => (uint)(i - 1)).ToArray(),
                    _biosignalStreamer.BiosignalSource.Frequency, Paradigm.Config.Test.Trial.Duration,
                    Paradigm.Config.Test.SsvepDelay);
                _biosignalStreamer.AttachConsumer(_hybridSsvepClassifier);
            }
        }

        private static double ConvertCosineValueToGrayScale(double cosVal) => (-cosVal + 1) / 2;

        protected override SpellerParadigm.Result CreateResult(Session session) => new SsvepResult();

        protected override void PostInitDirectXResources()
        {
            _frequencyTextFormat = new DW.TextFormat(DwFactory, "Consolas", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, Paradigm.Config.Gui.ButtonFontSize / 3.0F * 2.0F * ScaleFactor)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Far
            };
            var gradientStopCollection = new D2D1.GradientStopCollection(RenderTarget, new[]
            {
                new D2D1.GradientStop
                {
                    Color =  ButtonFlashingColor,
                    Position =  0,
                },
                new D2D1.GradientStop
                {
                    Color =  ButtonNormalColor,
                    Position =  1,
                },
            }, D2D1.ExtendMode.Clamp);
            _radialGradientBrush = new D2D1.RadialGradientBrush(RenderTarget, new D2D1.RadialGradientBrushProperties(), gradientStopCollection);
        }

        protected override void PreDestroyDirectXResources()
        {
            _radialGradientBrush.Dispose();
            _frequencyTextFormat.Dispose();
        }

        protected override void OnNextStage(StageChangedEventArgs e)
        {
            var stage = e.Stage;

            DisplayText = stage.Cue?.Trim();
            SubtitleText = stage.Subtitle?.Trim();

            if (stage.Marker != null)
            {
                /* Record marker */
                Markable?.Mark(stage.Marker.Value);

                var sessionTime = Session.SessionTime;

                /* Handle events */
                switch (stage.Marker)
                {
                    case MarkerDefinitions.BaselineStartMarker:
                        if (_hybridSsvepClassifier != null)
                        {
                            _initializer = _hybridSsvepClassifier?.CreateInitializer();
                            _biosignalStreamer.AttachConsumer(_initializer);
                        }
                        break;
                    case MarkerDefinitions.BaselineEndMarker:
                        if (_initializer != null)
                        {
                            _biosignalStreamer.DetachConsumer(_initializer);
                            _initializer.Initialize();
                            _initializer = null;
                        }
                        SpellerController.CalibrationComplete();
                        break;
                    case MarkerDefinitions.ParadigmStartMarker:
                        Result.ParadigmStartTime = sessionTime;
                        ParadigmStarted = true;
                        SpellerController.Start();
                        HintButton();
                        break;
                    case MarkerDefinitions.ParadigmEndMarker:
                        Result.ParadigmEndTime = sessionTime;
                        break;
                    case MarkerDefinitions.TrialStartMarker:
                    {
                        var trial = new SsvepTrial {TargetFrequencyIndex = HintedButton?.State};
                        var activedButtons = _activedButtons = UpdateCursor(GazePointHandler.CurrentPosition);
                        if (activedButtons != null)
                        {
                            var buttons = new LinkedList<SpellerParadigm.Result.Button>();
                            foreach (var activedButton in activedButtons)
                                if (activedButton != null)
                                    buttons.AddLast(new SsvepButton(activedButton.Key, activedButton.State));
                            trial.ActivedButtons = buttons;
                        }
                        trial.StartTime = CurrentTime;
                        _trial = trial;
                        if (_hybridSsvepClassifier != null)
                            _hybridSsvepClassifier.Actived = true;
                        SelectedButton = null;
                        DisplayText = null;
                        TrialCancelled = false;
                        break;
                    }
                    case MarkerDefinitions.TrialEndMarker:
                    {
                        var hintButton = HintedButton;
                        if (Paradigm.Config.Test.AlwaysCorrectFeedback)
                        {
                            SelectedButton = hintButton;
                            SelectionFeedbackCorrect = true;
                        }
                        HintButton(TrialCancelled ? 1 : 2);
                        var trial = _trial;
                        _trial = null;
                        trial.Cancelled = TrialCancelled;
                        trial.EndTime = CurrentTime;
                        if (_hybridSsvepClassifier != null) _hybridSsvepClassifier.Actived = false;
                        if (!trial.Cancelled && ComputeTrialResult(_activedButtons,
                                _hybridSsvepClassifier == null ? null : (Func<int>)_hybridSsvepClassifier.Classify, 
                                hintButton, out var button, out var correct))
                        {
                            trial.Correct = correct;
                            if (button != null)
                            {
                                trial.SelectedFrequencyIndex = button.State;
                                trial.SelectedButton = new SsvepButton(button.Key, button.State);
                            } else if (!Paradigm.Config.Test.AlwaysCorrectFeedback)
                                SystemSounds.Exclamation.Play();
                        }
                        CheckStop();
                        Result.Trials.Add(trial);
                        TrialTrigger?.Reset();
                        break;
                    }
                }
            }
        }

        protected override void OnDraw()
        {
            RenderTarget.Clear(BackgroundColor);

            if (ParadigmStarted)
            {
                DrawHintAndInput();

                var debug = Paradigm.Config.Test.Debug;
                var now = CurrentTime;
                var trial = _trial;
                var secsPassed = (now - trial?.StartTime) / 1000.0 ?? 0;
                /* Draw buttons */
                foreach (var button in Buttons)
                {
                    if (button == null) continue;
                    var scheme = button.State < 0 ? null : _stimulationPatterns[button.State];
                    var actived = scheme != null && trial != null;
                    if (button.BorderWidth > 0)
                    {
                        SharedBrush.Color = ButtonBorderColor;
                        RenderTarget.FillRectangle(button.BorderRect, SharedBrush);
                    }

                    SharedBrush.Color = ButtonNormalColor;
                    RenderTarget.FillRectangle(button.ContentRect, SharedBrush);

                    if (actived)
                    {
                        var progress = (float)Math.Max(0, Math.Min(scheme.Sample(secsPassed), 1));
                        var color = Color.SmoothStep(ButtonNormalColor, ButtonFlashingColor, progress);
                        //                        var radialGradientBrush = new D2D1.RadialGradientBrush(_renderTarget, new D2D1.RadialGradientBrushProperties
                        //                        {
                        //                            Center = button.Center,
                        //                            RadiusX = button.Size.X / 2,
                        //                            RadiusY = button.Size.Y / 2,
                        //                        }, new D2D1.GradientStopCollection(_renderTarget, new[]
                        //                        {
                        //                            new D2D1.GradientStop
                        //                            {
                        //                                Position = 0,
                        //                                Color = color
                        //                            },
                        //                            new D2D1.GradientStop
                        //                            {
                        //                                Position = 0.6F,
                        //                                Color = Color.SmoothStep(color, _buttonFlashingColor, 0.1F)
                        //                            },
                        //                            new D2D1.GradientStop
                        //                            {
                        //                                Position = 1,
                        //                                Color = _buttonNormalColor
                        //                            },
                        //                        }, D2D1.ExtendMode.Clamp));
                        SharedBrush.Color = color;
                        RenderTarget.FillRectangle(button.FlickerRect, SharedBrush);
                    }
                    else if (HintedButton == button)
                    {
                        SharedBrush.Color = ButtonHintColor;
                        RenderTarget.FillRectangle(button.FlickerRect, SharedBrush);
                    }

                    if (button.Key.Name.IsNotEmpty())
                    {
                        var brush = button == SelectedButton ? (SelectionFeedbackCorrect ? CorrectColorBrush : WrongColorBrush) : ForegroundBrush;
                        RenderTarget.DrawText(button.Key.Name, ButtonLabelTextFormat, button.ContentRect,
                            brush, D2D1.DrawTextOptions.None);
                    }
                    if (actived && button.FixationPointSize > 0)
                    {
                        SharedBrush.Color = ButtonFixationPointColor;
                        RenderTarget.FillEllipse(button.FixationPoint, SharedBrush);
                    }
                    if (debug && scheme != null)
                    {
                        SharedBrush.Color = new Color(ForegroundColor.ToVector3(), 0.6F);
                        RenderTarget.DrawText(scheme.ToString(), _frequencyTextFormat, button.ContentRect,
                            SharedBrush, D2D1.DrawTextOptions.None);
                    }
                }

            }

            DrawCueAndSubtitle();
        }

        private UIButton[] UpdateCursor(System.Drawing.Point? cursorNullable)
        {
            foreach (var button in Buttons)
                if (button != null)
                    button.State = -1;

            if (cursorNullable == null) return null;

            var activedButtonSlots = FindActivedButtons(cursorNullable.Value);
            for (var i = 0; i < activedButtonSlots.Length; i++)
            {
                var button = activedButtonSlots[i];
                if (button == null) continue;
                button.State = i;
            }
            return activedButtonSlots;
        }

    }
}
