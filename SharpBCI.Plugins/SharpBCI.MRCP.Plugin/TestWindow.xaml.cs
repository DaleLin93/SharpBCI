using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using System.Windows;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SharpBCI.Core.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JetBrains.Annotations;
using MarukoLib.UI;
using SharpBCI.Extensions;

namespace SharpBCI.Experiments.MRCP
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    internal partial class TestWindow
    {

        [NotNull] private readonly Session _session;

        [NotNull] private readonly MrcpExperiment _experiment;

        [CanBeNull] private readonly IMarkable _markable;

        [NotNull] private readonly StageProgram _stageProgram;

        private readonly IList<Line[]> _lineGroups = new List<Line[]>();

        private readonly Style _defaultLineStyle, _highlightedLineStyle;

        private readonly BitmapImage _relaxCueImage, _liftCueImage;

        private int _liftAtIndex, _putDownAtIndex;

        public TestWindow(Session session)
        {
            InitializeComponent();
            _session = session;
            _experiment = (MrcpExperiment) session.Experiment;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            this.HideCursorInside();

            _stageProgram = _experiment.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            _defaultLineStyle = (Style) FindResource("DefaultLineStyle");
            _highlightedLineStyle = (Style) FindResource("HighlightedLineStyle");

            _relaxCueImage = (BitmapImage)FindResource("RelaxCueImage");
            _liftCueImage = (BitmapImage)FindResource("LiftCueImage");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (e.Key)
            {
                case Key.Escape:
                    _markable?.Mark(MarkerDefinitions.UserExitMarker);
                    Stop(true);
                    break;
                case Key.Up:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.Y > currentCenter.Y);
                    break;
                case Key.Down:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.Y < currentCenter.Y);
                    break;
                case Key.Left:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.X < currentCenter.X);
                    break;
                case Key.Right:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.X > currentCenter.X);
                    break;
            }
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private void StageProgram_StageChanged(object sender, StageChangedEventArgs e)
        {
            /* Get next stage, exit on null (END REACHED) */
            if (!e.TryGetStage(out var stage))
            {
                this.DispatcherInvoke(() => Stop());
                return;
            }

            /* Record marker */
            if (stage.Marker != null)
                _markable?.Mark(stage.Marker.Value);

            this.DispatcherInvoke(() =>
            {
                /* Update text */
                CueTextBlock.Text = stage.Cue;

                /* Experiment start */
                if (stage.Marker == MarkerDefinitions.ExperimentStartMarker)
                    Container.Visibility = Visibility.Visible;

                /* Update stage */
                if (stage is MrcpStage mrcpStage)
                {
                    if (mrcpStage.IsInitialStage)
                    {
                        CueImage.Source = _relaxCueImage;

                        _lineGroups.Clear();
                        var stepX = PathCanvas.ActualWidth / mrcpStage.TotalTicks;
                        var middleY = PathCanvas.ActualHeight / 2;
                        for (var i = 0; i < mrcpStage.TotalTicks; i++)
                            _lineGroups.Add(new[] {new Line {X1 = stepX * i, Y1 = middleY, X2 = stepX * (i + 1), Y2 = middleY, Style = _defaultLineStyle}});
                        var liftYOffset = -PathCanvas.ActualHeight / 6;
                        var liftY = middleY + liftYOffset;
                        var lineIdx = _liftAtIndex = mrcpStage.LiftAt ?? 0;

                        /* Lifting */
                        for (var i = 0; i < 4; i++)
                        {
                            var line = _lineGroups[lineIdx++][0];
                            line.Y1 += i / 4.0 * liftYOffset;
                            line.Y2 += (i + 1) / 4.0 * liftYOffset;
                        }

                        for (var i = 0; i < 3; i++)
                        {
                            var line = _lineGroups[lineIdx++][0];
                            line.Y1 = line.Y2 = liftY;
                        }

                        /* Insert vertical line */
                        _putDownAtIndex = lineIdx;
                        var verticalLine = new Line {Y1 = liftY, Y2 = middleY, Style = _defaultLineStyle};
                        verticalLine.X1 = verticalLine.X2 = _lineGroups[_putDownAtIndex][0].X1;
                        _lineGroups[_putDownAtIndex] = new[] {_lineGroups[_putDownAtIndex][0], verticalLine};

                        /* Add lines to canvas */
                        PathCanvas.Children.Clear();
                        foreach (var lineGroup in _lineGroups)
                        foreach (var line in lineGroup)
                            PathCanvas.Children.Add(line);
                    }
                    foreach (var line in _lineGroups[mrcpStage.CurrentTick])
                        line.Style = _highlightedLineStyle;
                    if (mrcpStage.CurrentTick == _liftAtIndex)
                        CueImage.Source = _liftCueImage;
                    else if (mrcpStage.CurrentTick == _putDownAtIndex)
                        CueImage.Source = _relaxCueImage;
                }

                /* Set focus */
                if (!IsFocused) Focus();
            });
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _stageProgram.Stop();
            _session.Finish(null, userInterrupted);
        }

    }

}
