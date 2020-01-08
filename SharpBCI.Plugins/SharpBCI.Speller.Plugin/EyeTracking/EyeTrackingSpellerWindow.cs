using SharpBCI.Core.Staging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Media;
using System.Windows.Forms;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using D2D1 = SharpDX.Direct2D1;
using SharpDX;
using SharpBCI.Extensions;
using Point = System.Drawing.Point;
using SharpBCI.Core.IO;

namespace SharpBCI.Paradigms.Speller.EyeTracking
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    internal class EyeTrackingSpellerWindow : AbstractSpellerWindow
    {

        private readonly EyeTrackingDetector _detector;

        /* Paradigm variables */

        private volatile UIButton _activatedButton;

        private SpellerParadigm.Result.Trial _trial;

        public EyeTrackingSpellerWindow(Session session, SpellerController spellerController) : base(session, spellerController)
        {
            SuspendLayout();
            ControlBox = false;
            IsFullscreen = false;
            // ReSharper disable once VirtualMemberCallInConstructor
            DoubleBuffered = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(true);

            _detector = new EyeTrackingDetector();
            GazePointHandler.Point += (sender, e) =>
            {
                if (e != null)
                    _detector.Accept(e.Value);
            };

            SpellerController.CancellingTrial += (sender, e) =>
            {
                if (_trial != null)
                {
                    StageProgram.Skip();
                    TrialCancelled = true;
                }
            };
        }

        protected override void PostInitDirectXResources() { }

        protected override void PreDestroyDirectXResources() { }

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
                        var trial = new SpellerParadigm.Result.Trial();
                        var activedButton = _activatedButton = UpdateCursor(GazePointHandler.CurrentPosition);
                        if (activedButton != null)
                            trial.ActivedButtons = new SpellerParadigm.Result.Button(activedButton.Key).SingletonArray();
                        trial.StartTime = CurrentTime;
                        _trial = trial;
                        SelectedButton = null;
                        DisplayText = null;
                        TrialCancelled = false;
                        if (activedButton != null)
                            _detector.Active(activedButton.BorderRect);
                        break;
                    }
                    case MarkerDefinitions.TrialEndMarker:
                    {
                        var hintButton = HintedButton;
                        HintButton(TrialCancelled ? 1 : 2);
                        var trial = _trial;
                        var detectorResult = _detector.GetResult(Buttons);
                        _detector.Reset();
                        _trial = null;
                        trial.Cancelled = TrialCancelled;
                        trial.EndTime = CurrentTime;
                        if (!trial.Cancelled && ComputeTrialResult(Buttons, Functions.Constant(detectorResult), hintButton, 
                                out var button, out var correct))
                        {
                            trial.Correct = correct;
                            if (button != null)
                            {
                                trial.SelectedButton = new SpellerParadigm.Result.Button(button.Key);
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

                var now = CurrentTime;
                /* Draw buttons */
                foreach (var button in Buttons)
                {
                    if (button == null) continue;
                    var trial = _trial;
                    var actived = trial != null && _activatedButton == button;
                    if (button.BorderWidth > 0)
                    {
                        SharedBrush.Color = ButtonBorderColor;
                        RenderTarget.FillRectangle(button.BorderRect, SharedBrush);
                    }

                    SharedBrush.Color = ButtonNormalColor;
                    RenderTarget.FillRectangle(button.ContentRect, SharedBrush);

                    if (actived)
                    {
                        var highlightColor = ButtonFlashingColor;
                        var alpha = (now - trial.StartTime) / (float)Paradigm.Config.Test.Trial.Duration;
                        SharedBrush.Color = Color.SmoothStep(highlightColor, ButtonNormalColor, Math.Max(0, Math.Min(alpha, 1)));
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
                }

            }

            DrawCueAndSubtitle();
        }

        private UIButton UpdateCursor(Point? cursorNullable) => cursorNullable == null ? null : FindButtonAt(cursorNullable.Value);

    }
}
