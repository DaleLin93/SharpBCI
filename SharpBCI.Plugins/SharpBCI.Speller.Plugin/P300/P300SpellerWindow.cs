﻿using System;
using SharpBCI.Core.Staging;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Media;
using System.Windows.Forms;
using D2D1 = SharpDX.Direct2D1;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;
using MarukoLib.Lang.Sequence;

namespace SharpBCI.Paradigms.Speller.P300
{

    [SuppressMessage("ReSharper", "CollectionNeverQueried.Local")]
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    internal class P300SpellerWindow : AbstractSpellerWindow
    {

        private class P300Trial : SpellerParadigm.Result.Trial
        {

            public class SubTrial
            {

                public ulong Timestamp;

                public bool[] Flags;

            }

            public ICollection<SubTrial> SubTrials;

        }

        private readonly P300Detector _p300Detector;

        /* Paradigm variables */

        private volatile UIButton[] _activatedButtons;

        private volatile IRandomBools[] _randomBoolSequences;

        private P300Trial _trial;

        /* D3D Resources */

        public P300SpellerWindow(Session session, SpellerController spellerController) : base(session, spellerController)
        {
            SuspendLayout();
            ControlBox = false;
            IsFullscreen = true;
            // ReSharper disable once VirtualMemberCallInConstructor
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

            if (session.StreamerCollection.TryFindFirst<BiosignalStreamer>(out var biosignalStreamer))
                biosignalStreamer.AttachConsumer(_p300Detector = new P300Detector(
                    Paradigm.Config.Test.Channels.Enumerate(1, biosignalStreamer.BiosignalSource.ChannelNum).Select(i => (uint)(i - 1)).ToArray(),
                    biosignalStreamer.BiosignalSource.Frequency, (uint)biosignalStreamer.BiosignalSource.Frequency, 0.5F));
        }

        protected override void PostInitDirectXResources() { }

        protected override void PreDestroyDirectXResources() { }

        protected override void OnNextStage(StageChangedEventArgs e)
        {
            var stage = e.Stage;

            if (TrialCancelled && stage.Marker != null && stage.Marker == SpellerMarkerDefinitions.SubTrialMarker)
            {
                e.Action = StageAction.Skip;
                return;
            }

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
                        var trial = new P300Trial { SubTrials = new List<P300Trial.SubTrial>((int)(Paradigm.Config.Test.SubTrialCount + 1)) };
                        UpdateCursor(GazePointHandler.CurrentPosition);
                        var activedButtons = _activatedButtons;
                        if (activedButtons != null)
                        {
                            var buttons = new LinkedList<SpellerParadigm.Result.Button>();
                            foreach (var activedButton in activedButtons)
                                if (activedButton != null)
                                    buttons.AddLast(new SpellerParadigm.Result.Button(activedButton.Key));
                            trial.ActivedButtons = buttons;
                        }
                        trial.StartTime = CurrentTime;
                        _trial = trial;
                        if (_p300Detector != null)
                            _p300Detector.IsActive = true;
                        SelectedButton = null;
                        DisplayText = null;
                        TrialCancelled = false;
                        break;
                    }
                    case MarkerDefinitions.TrialEndMarker:
                    {
                        var hintButton = HintedButton;
                        HintButton(TrialCancelled ? 1 : 2);
                        var trial = _trial;
                        _trial = null;
                        trial.Cancelled = TrialCancelled;
                        trial.EndTime = CurrentTime;
                        if (_p300Detector != null) _p300Detector.IsActive = false;
                        if (!trial.Cancelled && ComputeTrialResult(_activatedButtons,
                                _p300Detector == null ? null : (Func<IdentificationResult>)_p300Detector.Compute,
                                hintButton, out var button, out var correct))
                        {
                            trial.Correct = correct;
                            if (button == null)
                                SystemSounds.Exclamation.Play();
                            else
                                trial.SelectedButton = new SpellerParadigm.Result.Button(button.Key);
                        }
                        CheckStop();
                        Result.Trials.Add(trial);
                        TrialTrigger?.Reset();
                        break;
                    }
                    case SpellerMarkerDefinitions.SubTrialMarker:
                    {
                        var activatedButtons = _activatedButtons;
                        var randomBoolSequences = _randomBoolSequences;
                        if (activatedButtons != null)
                        {
                            var flags = new bool[activatedButtons.Length];
                            for (var i = 0; i < activatedButtons.Length; i++)
                            {
                                var button = activatedButtons[i];
                                if (button != null)
                                {
                                    var flag = flags[i] = randomBoolSequences[i].Next();
                                    button.State = (flag) ? 1 : 0;
                                }
                            }
                            _trial.SubTrials.Add(new P300Trial.SubTrial {Timestamp = CurrentTime, Flags = flags});
                        }
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

                var trial = _trial;
                /* Draw buttons */
                foreach (var button in Buttons)
                {
                    if (button == null) continue;
                    if (trial != null && button.State < 0) continue;

                    if (button.BorderWidth > 0)
                    {
                        SharedBrush.Color = ButtonBorderColor;
                        RenderTarget.FillRectangle(button.BorderRect, SharedBrush);
                    }

                    SharedBrush.Color = ButtonNormalColor;
                    RenderTarget.FillRectangle(button.ContentRect, SharedBrush);

                    if (trial != null && button.State == 1)
                    {
                        SharedBrush.Color = ButtonFlashingColor;
                        RenderTarget.FillRectangle(button.FlickerRect, SharedBrush);
                    }
                    else if (HintedButton == button)
                    {
                        SharedBrush.Color = ButtonHintColor;
                        RenderTarget.FillRectangle(button.FlickerRect, SharedBrush);
                    }

                    if (button.Key.Name.IsNotEmpty())
                    {
                        SharedBrush.Color = button == SelectedButton ? (SelectionFeedbackCorrect ? CorrectTextColor : WrongTextColor) : ForegroundColor;
                        RenderTarget.DrawText(button.Key.Name, ButtonLabelTextFormat, button.ContentRect,
                            SharedBrush, D2D1.DrawTextOptions.None);
                    }
                }

            }

            DrawCueAndSubtitle();
        }

        private void UpdateCursor(System.Drawing.Point? cursorNullable)
        {
            foreach (var button in Buttons)
                if (button != null)
                    button.State = -1;

            if (cursorNullable == null) return;

            var activedButtonSlots = FindActivedButtons(cursorNullable.Value);
            foreach (var button in activedButtonSlots)
                if (button != null) 
                    button.State = 0;

            _randomBoolSequences = ArrayUtils.Initialize(activedButtonSlots.Length, index =>
                 Paradigm.Config.Test.TargetRate.CreateRandomBoolSequence((int)(DateTimeUtils.CurrentTimeTicks << 1 + index)));
            _activatedButtons = activedButtonSlots;
        }

    }
}
