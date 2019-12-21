using SharpBCI.Core.Staging;
using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Forms;
using Accord.Math;
using MarukoLib.DirectX;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using DW = SharpDX.DirectWrite;
using DXGI = SharpDX.DXGI;
using D2D1 = SharpDX.Direct2D1;
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using SharpDX;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;
using Point = System.Drawing.Point;
using RenderForm = SharpDX.Windows.RenderForm;
using Timer = System.Threading.Timer;

namespace SharpBCI.Paradigms.Speller
{

    internal class GazePointHandler : Core.IO.Consumer<Timestamped<IGazePoint>>
    {

        public event EventHandler<Point?> Point;

        public Point? CurrentPosition { get; private set; }

        public override void Accept(Timestamped<IGazePoint> value)
        {
            var gazePoint = value.Value;
            CurrentPosition = new Point((int)Math.Round(gazePoint.X), (int)Math.Round(gazePoint.Y));
            Point?.Invoke(this, CurrentPosition);
        }

    }

    internal interface ITrialTrigger : IDisposable
    {

        void Start();

        void Stop();

        void Reset();

    }

    internal class ButtonInsideTrialTrigger : ITrialTrigger
    {

        private readonly IClock _clock;

        private readonly SpellerController _spellerController;

        private readonly GazePointHandler _gazePointHandler;

        private readonly bool _cancellable;

        private readonly uint _minTrialInterval;

        private readonly uint _hoverToSelectDuration;

        private readonly Func<Point, AbstractSpellerWindow.UIButton> _findButtonFunc;

        private Timer _timer;

        private long _startAt;

        private long _hoverStartAt;

        private bool _inTrial;

        private AbstractSpellerWindow.UIButton _previousHover;

        public ButtonInsideTrialTrigger(IClock clock, SpellerController spellerController, GazePointHandler gazePointHandler,
            SpellerParadigm.Configuration.TestConfig testConfig, Func<Point, AbstractSpellerWindow.UIButton> findButtonFunc)
        {
            _clock = clock;
            _spellerController = spellerController;
            _gazePointHandler = gazePointHandler;
            _cancellable = testConfig.TrialCancellable;
            _minTrialInterval = testConfig.Trial.Interval;
            _hoverToSelectDuration = testConfig.SelectionDelay;
            _findButtonFunc = findButtonFunc;

            spellerController.Starting += (sender, e) => Start();
            spellerController.Stopping += (sender, e) => Stop();

            Reset();
        }

        public void Start()
        {
            _timer?.Dispose();
            Reset();
            _timer = new Timer(Tick, null, 100, 100);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Reset()
        {
            _startAt = _clock.Time;
            _previousHover = null;
            _inTrial = false;
        }

        public void Dispose() => Stop();

        private void Tick(object state)
        {
            var position = _gazePointHandler.CurrentPosition;
            if (position == null)
            {
                Reset();
                return;
            }
            var now = _clock.Time;
            var button = _findButtonFunc(position.Value);
            if (button != _previousHover)
            {
                _previousHover = button;
                _hoverStartAt = now;
                if (_cancellable && _inTrial)
                {
                    _spellerController.CancelTrial();
                    Reset();
                }
                return;
            }
            if (button == null || _inTrial) return;
            if (_clock.Unit.ToMilliseconds(Math.Abs(now - _startAt)) < _minTrialInterval) return;
            if (_clock.Unit.ToMilliseconds(Math.Abs(now - _hoverStartAt)) < _hoverToSelectDuration) return;
            _inTrial = true;
            _spellerController.CreateTrial();
        }

    }

    internal class DwellTrialTrigger : ITrialTrigger
    {

        private readonly IClock _clock;

        private readonly SpellerController _spellerController;

        private readonly GazePointHandler _gazePointHandler;

        private readonly bool _cancellable;

        private readonly uint _minTrialInterval;

        private readonly uint _cursorMovementTolerance;

        private readonly uint _hoverToSelectDuration;

        private long _startAt;

        private Timer _timer;

        private Timestamped<Point>? _point;

        private bool _inTrial;

        public DwellTrialTrigger(IClock clock, SpellerController spellerController, GazePointHandler gazePointHandler, SpellerParadigm.Configuration.TestConfig testConfig)
        {
            _clock = clock;
            _spellerController = spellerController;
            _gazePointHandler = gazePointHandler;
            _cancellable = testConfig.TrialCancellable;
            _minTrialInterval = testConfig.Trial.Interval;
            _cursorMovementTolerance = testConfig.CursorMovementTolerance;
            _hoverToSelectDuration = testConfig.SelectionDelay;

            spellerController.Starting += (sender, e) => Start();
            spellerController.Stopping += (sender, e) => Stop();

            Reset();
        }

        public void Start()
        {
            _timer?.Dispose();
            Reset();
            _timer = new Timer(Tick, null, 100, 100);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Reset()
        {
            _startAt = _clock.Time;
            _point = null;
            _inTrial = false;
        }

        public void Dispose() => Stop();

        private void Tick(object state)
        {
            var position = _gazePointHandler.CurrentPosition;
            if (position == null)
            {
                _point = null;
                return;
            }
            var now = _clock.Time;
            if (_point == null)
            {
                _point = new Timestamped<Point>(now, position.Value);
                return;
            }
            var p1 = _point.Value.Value;
            var p2 = position.Value;
            if (Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y) > _cursorMovementTolerance)
            {
                _point = new Timestamped<Point>(now, position.Value);
                if (_cancellable && _inTrial) _spellerController.CancelTrial();
                return;
            }
            if (_inTrial) return;
            if (_clock.Unit.ToMilliseconds(Math.Abs(now - _startAt)) < _minTrialInterval) return;
            if (_clock.Unit.ToMilliseconds(Math.Abs(now - _point.Value.Timestamp)) < _hoverToSelectDuration) return;
            _point = new Timestamped<Point>(now, position.Value);
            _inTrial = true;
            _spellerController.CreateTrial();
        }

    }

    internal abstract class AbstractSpellerWindow : RenderForm, IDisposable
    {

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        internal class UIButton
        {

            public UIButton(int index, int row, int col, KeyDescriptor key, float borderWidth, float fixationPointSize, Margins flickerMargins)
            {
                Index = index;
                Row = row;
                Col = col;
                Key = key;
                BorderWidth = borderWidth;
                FixationPointSize = fixationPointSize;
                FlickerMargins = flickerMargins;
            }

            public int Index { get; }

            public int Row { get; }

            public int Col { get; }

            public KeyDescriptor Key { get; }

            public float BorderWidth { get; }

            public float FixationPointSize { get; }

            public Margins FlickerMargins { get; }

            public RawVector2 Center { get; private set; }

            public RawVector2 Size { get; private set; }

            public int State { get; set; }

            public RawRectangleF BorderRect { get; private set; }

            public RawRectangleF ContentRect { get; private set; }

            public RawRectangleF FlickerRect { get; private set; }

            public D2D1.Ellipse FixationPoint { get; private set; }

            public void UpdateGeometries(RawVector2 center, RawVector2 size)
            {
                Center = center;
                Size = size;

                BorderRect = SharpDXUtils.CenteredRect(center, size);
                ContentRect = BorderWidth > 0 ? BorderRect.Shrink(BorderWidth) : BorderRect;

                var absoluteMargins = FlickerMargins.GetAbsolute(ContentRect.Width(), ContentRect.Height());
                FlickerRect = absoluteMargins.IsEmpty(0.1) ? ContentRect
                    : ContentRect.Shrink((float)absoluteMargins.Left, (float)absoluteMargins.Top, (float)absoluteMargins.Right, (float)absoluteMargins.Bottom);

                FixationPoint = new D2D1.Ellipse(FlickerRect.Center(), FixationPointSize, FixationPointSize);
            }

        }

        private class CustomColorRenderer : DW.TextRendererBase
        {
            private D2D1.RenderTarget _renderTarget;
            private D2D1.SolidColorBrush _defaultBrush;

            public void AssignResources(D2D1.RenderTarget renderTarget, D2D1.SolidColorBrush defaultBrush)
            {
                _renderTarget = renderTarget;
                _defaultBrush = defaultBrush;
            }

            public override SharpDX.Result DrawGlyphRun(object clientDrawingContext, float baselineOriginX, float baselineOriginY, D2D1.MeasuringMode measuringMode, 
                DW.GlyphRun glyphRun, DW.GlyphRunDescription glyphRunDescription, ComObject effect)
            {
                var sb = _defaultBrush;
                if (effect != null && effect is D2D1.SolidColorBrush solidColorBrush) sb = solidColorBrush;
                try
                {
                    _renderTarget.DrawGlyphRun(new Vector2(baselineOriginX, baselineOriginY), glyphRun, sb, measuringMode);
                    return SharpDX.Result.Ok;
                }
                catch
                {
                    return SharpDX.Result.Fail;
                }
            }
        }

        private readonly object _renderContextLock = new object();

        private readonly CustomColorRenderer _customColorRenderer = new CustomColorRenderer();

        protected readonly Session Session;

        protected readonly SpellerController SpellerController;

        protected readonly SpellerParadigm Paradigm;

        protected readonly IMarkable Markable;

        protected readonly StageProgram StageProgram;

        protected readonly GazePointHandler GazePointHandler;

        protected SpellerParadigm.Result Result;

        protected ITrialTrigger TrialTrigger;

        /* Paradigm */

        protected readonly string HintText;

        protected readonly int[] LayoutSize;

        protected readonly RawVector2[,] ButtonCenterMatrix;

        protected readonly UIButton[,] ButtonMatrix;

        protected readonly UIButton[] Buttons;

        protected readonly IDictionary<char, UIButton> Char2ButtonDict;

        protected float ScaleFactor;

        protected float InputBoxHeight;

        protected UIButton SelectedButton;

        protected bool SelectionFeedbackCorrect;

        protected UIButton HintedButton;

        protected string InputText = "";

        protected bool UserInterrupted;

        protected bool ParadigmStarted;

        protected bool TrialCancelled;

        protected string DisplayText;

        protected string SubtitleText;

        /* Converted variables */

        protected readonly Color BackgroundColor, ForegroundColor;

        protected readonly Color CorrectTextColor, WrongTextColor;

        protected readonly Color ButtonBorderColor, ButtonNormalColor, ButtonFlashingColor, ButtonHintColor;

        protected readonly Color ButtonFixationPointColor;

        /* D3D Resources */

        private readonly DXGI.PresentParameters _presentParameters = new DXGI.PresentParameters();

        protected D3D11.Device D3DDevice;

        protected D3D11.DeviceContext D3DDeviceContext;

        protected DXGI.SwapChain1 SwapChain;

        protected D3D11.RenderTargetView RenderTargetView;

        protected D2D1.Factory D2DFactory;

        protected D2D1.RenderTarget RenderTarget;

        protected DW.Factory DwFactory;

        protected DW.TextFormat CueTextFormat, SubtitleTextFormat, ButtonLabelTextFormat, InputTextFormat;

        protected D2D1.SolidColorBrush SharedBrush, BackgroundBrush, ForegroundBrush, CorrectColorBrush, WrongColorBrush;

        protected DW.TextLayout InputTextLayout;

        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        protected AbstractSpellerWindow(Session session, SpellerController spellerController)
        {
            // ReSharper disable once LocalizableElement
            Text = "Speller";
            SuspendLayout();
            ControlBox = false;
            IsFullscreen = true;
            DoubleBuffered = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(true);

            Load += Window_OnLoaded;
            KeyUp += Window_OnKeyUp;
            Resize += Window_OnResize;

            Session = session;
            SpellerController = spellerController;
            Paradigm = (SpellerParadigm) session.Paradigm;

            Markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();
            LayoutSize = Paradigm.Config.Test.Layout.GetLayoutSize(Paradigm.Config.Gui.ColumnsOverridden);
            var maxButtonNum = LayoutSize[0] * LayoutSize[1];
            Buttons = new UIButton[maxButtonNum];
            var index = 0;
            var scaleFactor = (float) GraphicsUtils.Scale;
            foreach (var key in Paradigm.Config.Test.Layout.Keys)
            {
                if (index > maxButtonNum) break;
                var button = new UIButton(index, index / LayoutSize[1], index % LayoutSize[1], key,
                    (float)Paradigm.Config.Gui.ButtonBorder.Width * scaleFactor,
                    Paradigm.Config.Gui.ButtonFixationPoint.Size * scaleFactor,
                    Paradigm.Config.Gui.ButtonFlashingMargins * (Paradigm.Config.Gui.ButtonFlashingMargins.Relative ? 1 : scaleFactor))
                { State = -1 };
                var size = Paradigm.Config.Gui.ButtonSize * scaleFactor;
                button.UpdateGeometries(new RawVector2(), new RawVector2(size, size));
                Buttons[index++] = button;
            }
            ButtonMatrix = Buttons.Reshape(LayoutSize[0], LayoutSize[1], MatrixOrder.CRowMajor);
            ButtonCenterMatrix = new RawVector2[LayoutSize[0], LayoutSize[1]];
            Char2ButtonDict = new Dictionary<char, UIButton>();
            foreach (var button in Buttons)
                if (button?.Key?.InputChar != null)
                {
                    var ch = button.Key.InputChar.Value;
                    if (Char2ButtonDict.ContainsKey(ch)) throw new Exception($"duplicated char: '{ch}'");
                    Char2ButtonDict[button.Key.InputChar.Value] = button;
                }

            StageProgram = Paradigm.CreateStagedProgram(session, spellerController);
            StageProgram.StageChanged += (sender, e) =>
            {
                if (e.IsEndReached) this.ControlInvoke(self => Stop());
                else OnNextStage(e);
            };

            GazePointHandler = new GazePointHandler();
            if (session.StreamerCollection.TryFindFirst<GazePointStreamer>(out var gazePointStreamer))
            {
                gazePointStreamer.AttachConsumer(GazePointHandler);
                // if (_paradigm.Config.Test.TrialCancellable) TODO: blink to cancel
                if (gazePointStreamer.EyeTracker.GetType() != typeof(CursorTracker)) this.HideCursorInside();
            }
            else
                throw new StateException("gaze point streamer not found for GazePointController");


            if (Paradigm.Config.Test.DynamicInterval)
            {
                if (Paradigm.Config.Test.ActivationMode == SpellerActivationMode.Single)
                    TrialTrigger = new ButtonInsideTrialTrigger(session.Clock, SpellerController, GazePointHandler, Paradigm.Config.Test, FindButtonAt);
                else
                    TrialTrigger = new DwellTrialTrigger(session.Clock, SpellerController, GazePointHandler, Paradigm.Config.Test);
            }

            HintText = Paradigm.Config.Test.HintText;

            /* Type conversion */
            BackgroundColor = Paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            ForegroundColor = Paradigm.Config.Gui.ForegroundColor.ToSdColor().ToSdx();
            CorrectTextColor = Paradigm.Config.Gui.CorrectTextColor.ToSdColor().ToSdx();
            WrongTextColor = Paradigm.Config.Gui.WrongTextColor.ToSdColor().ToSdx();
            ButtonBorderColor = Paradigm.Config.Gui.ButtonBorder.Color.ToSdColor().ToSdx();
            ButtonNormalColor = Paradigm.Config.Gui.ButtonNormalColor.ToSdColor().ToSdx();
            ButtonFlashingColor = Paradigm.Config.Gui.ButtonFlashingColor.ToSdColor().ToSdx();
            ButtonHintColor = Paradigm.Config.Gui.ButtonHintColor.ToSdColor().ToSdx();
            ButtonFixationPointColor = Paradigm.Config.Gui.ButtonFixationPoint.Color.ToSdColor().ToSdx();
        }

        protected ulong CurrentTime => Session.SessionTime;

        public new void Show()
        {
            ((Control) this).Show();
            RenderLoop.Run(this, OnRender);
        }

        public new void Dispose()
        {
            TrialTrigger?.Dispose();
            lock (_renderContextLock)
                DisposeDirectXResources();
            base.Dispose();
        }

        protected virtual SpellerParadigm.Result CreateResult(Session session) => new SpellerParadigm.Result();

        protected void InitializeDirectXResources()
        {
            ScaleFactor = (float) GraphicsUtils.Scale;

            var clientSize = ClientSize;
            var backBufferDesc = new DXGI.ModeDescription(clientSize.Width, clientSize.Height,
                new DXGI.Rational(60, 1), DXGI.Format.R8G8B8A8_UNorm);

            var swapChainDesc = new DXGI.SwapChainDescription()
            {
                ModeDescription = backBufferDesc,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Usage = DXGI.Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = Handle,
                SwapEffect = DXGI.SwapEffect.Discard,
                IsWindowed = Paradigm.Config.Test.Debug
            };

            D3D11.Device.CreateWithSwapChain(D3D.DriverType.Hardware, D3D11.DeviceCreationFlags.BgraSupport,
                new[] { D3D.FeatureLevel.Level_10_0 }, swapChainDesc, out D3DDevice, out var swapChain);
            D3DDeviceContext = D3DDevice.ImmediateContext;

            SwapChain = new DXGI.SwapChain1(swapChain.NativePointer);

            D2DFactory = new D2D1.Factory();

            using (var backBuffer = SwapChain.GetBackBuffer<D3D11.Texture2D>(0))
            {
                RenderTargetView = new D3D11.RenderTargetView(D3DDevice, backBuffer);
                RenderTarget = new D2D1.RenderTarget(D2DFactory, backBuffer.QueryInterface<DXGI.Surface>(),
                    new D2D1.RenderTargetProperties(new D2D1.PixelFormat(DXGI.Format.Unknown, D2D1.AlphaMode.Premultiplied)))
                {
                    TextAntialiasMode = D2D1.TextAntialiasMode.Cleartype
                };
            }

            DwFactory = new DW.Factory(DW.FactoryType.Shared);

            _customColorRenderer.AssignResources(RenderTarget, ForegroundBrush);

            CueTextFormat = new DW.TextFormat(DwFactory, "Arial", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, 120 * ScaleFactor)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            SubtitleTextFormat = new DW.TextFormat(DwFactory, "Consolas", DW.FontWeight.Light,
                DW.FontStyle.Normal, DW.FontStretch.Normal, Paradigm.Config.Gui.InputTextFontSize * ScaleFactor / 2)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            ButtonLabelTextFormat = new DW.TextFormat(DwFactory, "Consolas", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, Paradigm.Config.Gui.ButtonFontSize * ScaleFactor)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            InputTextFormat = new DW.TextFormat(DwFactory, "Consolas", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, Paradigm.Config.Gui.InputTextFontSize * ScaleFactor)
            {
                TextAlignment = DW.TextAlignment.Leading,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };

            SharedBrush = new D2D1.SolidColorBrush(RenderTarget, Color.White);
            BackgroundBrush = new D2D1.SolidColorBrush(RenderTarget, BackgroundColor);
            ForegroundBrush = new D2D1.SolidColorBrush(RenderTarget, ForegroundColor);
            CorrectColorBrush = new D2D1.SolidColorBrush(RenderTarget, CorrectTextColor);
            WrongColorBrush = new D2D1.SolidColorBrush(RenderTarget, WrongTextColor);

            PostInitDirectXResources();
        }

        protected void DisposeDirectXResources()
        {
            PreDestroyDirectXResources();
            SharedBrush.Dispose();
            BackgroundBrush.Dispose();
            ForegroundBrush.Dispose();
            CorrectColorBrush.Dispose();
            WrongColorBrush.Dispose();

            InputTextFormat.Dispose();
            ButtonLabelTextFormat.Dispose();
            SubtitleTextFormat.Dispose();
            CueTextFormat.Dispose();

            DwFactory.Dispose();
            RenderTarget.Dispose();
            RenderTargetView.Dispose();
            D2DFactory.Dispose();
            SwapChain.Dispose();
            D3DDeviceContext.Dispose();
            D3DDevice.Dispose();
        }

        /// <returns>true if result is computed</returns>
        protected bool ComputeTrialResult(UIButton[] activatedButtons, Func<IdentificationResult> func, UIButton hintButton, out UIButton button, out bool? correct)
        {
            button = null;
            correct = null;
            if (activatedButtons == null) return false;
            var result = func?.Invoke() ?? IdentificationResult.Missed;
            var selectedButton = !result.IsValidResult(activatedButtons.Length) ? null : activatedButtons[result.FrequencyIndex];
            button = selectedButton;
            correct = OnButtonSelected(hintButton, selectedButton);
            return true;
        }

        protected bool? OnButtonSelected(UIButton hintButton, UIButton button)
        {
            bool? returns;
            if (!Paradigm.Config.Test.AlwaysCorrectFeedback)
            {
                SelectedButton = button;
                SelectionFeedbackCorrect = hintButton == null || hintButton == button;
                returns = hintButton == null ? null : (bool?) SelectionFeedbackCorrect;
            }
            else
            {
                SelectedButton = hintButton ?? button;
                SelectionFeedbackCorrect = true;
                returns = hintButton == null ? null : (bool?) (hintButton == button);
            }
            if (button != null) InputText = button.Key.Operator(InputText ?? "") ?? "";
            else if (hintButton != null) InputText = (InputText ?? "") + '_';
            UpdateInputTextLayout();
            return returns;
        }

        protected void UpdateInputTextLayout()
        {
            if (InputText?.IsEmpty() ?? true)
                InputTextLayout = null;
            else
            {
                var textLayout = new DW.TextLayout(DwFactory, InputText, InputTextFormat, 0, 0);
                if (HintText != null)
                {
                    var len = Math.Min(InputText.Length, HintText.Length);
                    for (var i = 0; i < len; i++)
                    {
                        var brush = InputText[i] == HintText[i] ? CorrectColorBrush : WrongColorBrush;
                        textLayout.SetDrawingEffect(brush, new DW.TextRange(i, 1));
                    }
                }
                InputTextLayout = textLayout;
            }
        }

        protected void Stop()
        {
            SwapChain.IsFullScreen = false;
            Dispose();
            Close();
            StageProgram.Stop();
            Result.HintText = HintText;
            Result.InputText = InputText;
            Session.Finish(Result, UserInterrupted);
        }

        protected void CheckStop()
        {
            if (HintText != null && HintText.Length <= InputText.Length)
                SpellerController.Stop();
        }

        protected void HintButton(int offset = 1)
        {
            if (HintText != null && HintText.Length >= InputText.Length + offset)
                HintedButton = Char2ButtonDict[HintText[InputText.Length + offset - 1]];
            else
                HintedButton = null;
        }

        protected UIButton[] FindActivedButtons(Point point)
        {
            switch (Paradigm.Config.Test.ActivationMode)
            {
                case SpellerActivationMode.Single:
                    return new[] {FindNearestButton(point)};
                case SpellerActivationMode.TwoByTwoBlock:
                    return Find2X2ButtonMatrix(point).Reshape(MatrixOrder.CRowMajor);
                case SpellerActivationMode.ThreeByThreeBlock:
                    return Find3X3ButtonMatrix(point).Reshape(MatrixOrder.CRowMajor);
                default:
                    return (UIButton[])Buttons.Clone();
            }
        }

        protected UIButton FindButtonAt(Point point)
        {
            var rows = ButtonMatrix.GetLength(0);
            var cols = ButtonMatrix.GetLength(1);
            for (var r = 0; r < rows; r++)
            {
                var firstInRow = ButtonMatrix[r, 0];
                if (firstInRow == null) continue;
                if (point.Y > firstInRow.BorderRect.Bottom) continue;
                if (point.Y < firstInRow.BorderRect.Top) return null;
                for (var c = 0; c < cols; c++)
                {
                    var button = ButtonMatrix[r, c];
                    if (button == null) continue;
                    if (point.X > button.BorderRect.Right) continue;
                    return point.X < button.BorderRect.Left ? null : button;
                }
            }
            return null;
        }

        protected UIButton FindNearestButton(Point point)
        {
            UIButton nearestButton = null;
            var nearestDistance = double.PositiveInfinity;
            foreach (var button in Buttons)
            {
                if (button == null) continue;
                var manhattan = Math.Abs(button.Center.X - point.X) + Math.Abs(button.Center.Y - point.Y);
                if (nearestDistance > manhattan)
                {
                    nearestButton = button;
                    nearestDistance = manhattan;
                }
            }
            return nearestButton;
        }

        protected UIButton[,] Find2X2ButtonMatrix(Point point)
        {
            var rows = LayoutSize[0];
            var cols = LayoutSize[1];
            var buttonMatrix = new UIButton[2, 2];
            if (rows == 0 || cols == 0) return buttonMatrix;
            if (rows == 1 && cols == 1)
            {
                buttonMatrix[0, 0] = ButtonMatrix[0, 0];
                return buttonMatrix;
            }
            var rowStart = rows == 1 ? 0 : 0.5;
            var colStart = cols == 1 ? 0 : 0.5;
            double targetRow = 0, targetCol = 0, targetDist = double.PositiveInfinity;
            for (var c = 0; c < cols - 1; c++)
            {
                var col = colStart + c;
                for (var r = 0; r < rows - 1; r++)
                {
                    var row = rowStart + r;
                    var lt = ButtonCenterMatrix[(int) Math.Floor(row), (int) Math.Floor(col)];
                    var rt = ButtonCenterMatrix[(int) Math.Floor(row), (int) Math.Ceiling(col)];
                    var lb = ButtonCenterMatrix[(int) Math.Ceiling(row), (int) Math.Floor(col)];
                    var blockCenter = new RawVector2((lt.X + rt.X) / 2, (lt.Y + lb.Y) / 2);
                    var distance = Math.Abs(blockCenter.X - point.X) + Math.Abs(blockCenter.Y - point.Y);
                    if (distance < targetDist)
                    {
                        targetRow = row;
                        targetCol = col;
                        targetDist = distance;
                    }
                }
            }
            if (rows == 1)
            {
                buttonMatrix[0, 0] = ButtonMatrix[(int)Math.Floor(targetRow), (int)Math.Floor(targetCol)];
                buttonMatrix[0, 1] = ButtonMatrix[(int)Math.Floor(targetRow), (int)Math.Ceiling(targetCol)];
            }
            else if (cols == 1)
            {
                buttonMatrix[0, 0] = ButtonMatrix[(int)Math.Floor(targetRow), (int)Math.Floor(targetCol)];
                buttonMatrix[1, 0] = ButtonMatrix[(int)Math.Ceiling(targetRow), (int)Math.Floor(targetCol)];
            }
            else
            {
                buttonMatrix[0, 0] = ButtonMatrix[(int)Math.Floor(targetRow), (int)Math.Floor(targetCol)];
                buttonMatrix[0, 1] = ButtonMatrix[(int)Math.Floor(targetRow), (int)Math.Ceiling(targetCol)];
                buttonMatrix[1, 0] = ButtonMatrix[(int)Math.Ceiling(targetRow), (int)Math.Floor(targetCol)];
                buttonMatrix[1, 1] = ButtonMatrix[(int)Math.Ceiling(targetRow), (int)Math.Ceiling(targetCol)];
            }
            return buttonMatrix;
        }

        protected UIButton[,] Find3X3ButtonMatrix(Point point)
        {
            var nearestButton = FindNearestButton(point);
            if (nearestButton == null) return null;
            var buttonMatrix = new UIButton[3, 3];
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = nearestButton.Col + dx;
                var y = nearestButton.Row + dy;
                if (x < 0 || y < 0 || x >= LayoutSize[1] || y >= LayoutSize[0]) continue;
                var index = LayoutSize[1] * y + x;
                buttonMatrix[dy + 1, dx + 1] = Buttons[index];
            }
            return buttonMatrix;
        }

        protected void DrawHintAndInput()
        {
            if (!Paradigm.Config.Gui.InputBarVisibility) return;

            /* Draw Hint */
            if (HintText != null)
            {
                SharedBrush.Color = new Color(ForegroundColor.R, ForegroundColor.G, ForegroundColor.B, 0.7F);
                RenderTarget.DrawText(HintText, InputTextFormat, new RawRectangleF(10, 0, Width - 10, InputBoxHeight / 2),
                    SharedBrush, D2D1.DrawTextOptions.None);
            }

            /* Draw user input text */
            if (InputTextLayout != null)
            {
                var rect = HintText == null
                    ? new RawRectangleF(10, 5, Width - 10, InputBoxHeight - 5)
                    : new RawRectangleF(10, InputBoxHeight / 2, Width - 10, InputBoxHeight);
                InputTextLayout.MaxWidth = rect.Right - rect.Left;
                InputTextLayout.MaxHeight = rect.Bottom - rect.Top;
                InputTextLayout.Draw(_customColorRenderer, rect.Left, rect.Top);
            }

            /* Draw Input Box Bottom Line */
            SharedBrush.Color = ForegroundColor;
            RenderTarget.DrawLine(new RawVector2(10, InputBoxHeight + 2), new RawVector2(Width - 10, InputBoxHeight + 2), SharedBrush, 2);
        }

        protected void DrawCueAndSubtitle()
        {
            if (DisplayText?.IsNotEmpty() ?? false) // Draw text
            {
                RenderTarget.DrawText(DisplayText, CueTextFormat, new RawRectangleF(0, 0, Width, Height),
                    ForegroundBrush, D2D1.DrawTextOptions.None);
            }
            if (SubtitleText?.IsNotEmpty() ?? false) // Draw subtitle
            {
                RenderTarget.DrawText(SubtitleText, SubtitleTextFormat, new RawRectangleF(10, Height - InputBoxHeight - 10, Width - 10, Height - 10),
                    ForegroundBrush, D2D1.DrawTextOptions.None);
            }
        }

        protected abstract void PostInitDirectXResources();

        protected abstract void PreDestroyDirectXResources();

        protected abstract void OnDraw();

        protected abstract void OnNextStage(StageChangedEventArgs e);

        private void OnRender()
        {
            lock (_renderContextLock)
            {
                if (RenderTarget?.IsDisposed ?? true) return;

                RenderTarget.BeginDraw();
                OnDraw();
                RenderTarget.EndDraw();

                SwapChain.Present(1, DXGI.PresentFlags.None, _presentParameters);
            }
        }

        private void UpdateLayout()
        {
            InputBoxHeight = Paradigm.Config.Gui.InputBarVisibility 
                ? Paradigm.Config.Gui.InputTextFontSize * ScaleFactor * 2 : 0;
            var rows = LayoutSize[0];
            var cols = LayoutSize[1];
            var buttonSize = Paradigm.Config.Gui.ButtonSize * ScaleFactor;
            var buttonMargin = Paradigm.Config.Gui.ButtonMargin * ScaleFactor;
            float winW = Width, winH = Height - InputBoxHeight;
            var buttonFrameW = winW / cols;
            var buttonFrameH = winH / rows;

            for (var col = 0; col < cols; col++)
            for (var row = 0; row < rows; row++)
            {
                var button = ButtonMatrix[row, col];
                if (button == null) continue;

                var center = ButtonCenterMatrix[row, col] = new RawVector2(buttonFrameW * (col + 0.5F), InputBoxHeight + buttonFrameH * (row + 0.5F));

                double actualWidth, actualHeight;
                if (buttonSize <= 0)
                {
                    actualWidth = buttonFrameW - 2 * buttonMargin;
                    actualHeight = buttonFrameH - 2 * buttonMargin;
                }
                else
                {
                    actualWidth = buttonSize;
                    actualHeight = buttonSize;
                }
                actualWidth = Math.Max(Math.Min(actualWidth, buttonFrameW), 0);
                actualHeight = Math.Max(Math.Min(actualHeight, buttonFrameH), 0);
                var size = new RawVector2((float)actualWidth, (float)actualHeight);

                button.UpdateGeometries(center, size);
            }
        }
        
        private void Window_OnLoaded(object sender, EventArgs e)
        {
            lock (_renderContextLock)
                InitializeDirectXResources();
            UpdateLayout();

            Result = CreateResult(Session);
            Result.Buttons = Paradigm.Config.Test.Layout.Keys.Select(key => new SpellerParadigm.Result.Button(key)).ToArray();
            Result.Trials = new LinkedList<SpellerParadigm.Result.Trial>();

            Session.Start();
            StageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            Markable?.Mark(MarkerDefinitions.UserExitMarker);
            TrialTrigger?.Stop();
            UserInterrupted = true;
            SpellerController.CancelTrial();
            SpellerController.Stop();
            if (!ParadigmStarted) Stop();
        }

        private void Window_OnResize(object sender, EventArgs e)
        {
            lock (_renderContextLock)
            {
                if (D3DDeviceContext?.IsDisposed ?? true)
                    return;
                DisposeDirectXResources();
                InitializeDirectXResources();
            }
            UpdateLayout();
        }
        
    }
}
