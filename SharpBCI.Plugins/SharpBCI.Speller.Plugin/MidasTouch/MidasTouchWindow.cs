using SharpBCI.Core.Staging;
using SharpDX.Windows;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using MarukoLib.DirectX;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using DW = SharpDX.DirectWrite;
using DXGI = SharpDX.DXGI;
using D2D1 = SharpDX.Direct2D1;
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using SharpDX;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using RenderForm = SharpDX.Windows.RenderForm;

namespace SharpBCI.Paradigms.Speller.MidasTouch
{

    internal class MidasTouchWindow : RenderForm, IDisposable
    {

        private readonly object _renderContextLock = new object();

        private readonly Session _session;

        private readonly MidasTouchParadigm _paradigm;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        /* Paradigm variables */

        private bool _paradigmStarted = false;

        private bool _trialStarted = false;

        private string _displayText = null;

        /* Converted variables */

        private readonly Color _backgroundColor;

        private readonly Color _foregroundColor;

        private readonly Color _blockBorderColor;

        private readonly Color _blockNormalColor;

        /* D3D Resources */

        private readonly DXGI.PresentParameters _presentParameters = new DXGI.PresentParameters();
        
        private D3D11.Device _d3DDevice;

        private D3D11.DeviceContext _d3DDeviceContext;

        private DXGI.SwapChain1 _swapChain;

        private D3D11.RenderTargetView _renderTargetView;

        private D2D1.Factory _d2DFactory;

        private D2D1.RenderTarget _renderTarget;

        private D2D1.SolidColorBrush _solidColorBrush;

        private DW.Factory _dwFactory;

        private DW.TextFormat _textFormat;

        public MidasTouchWindow(Session session)
        {
            // ReSharper disable once LocalizableElement
            Text = "Midas Touch";
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
            this.HideCursorInside(); 

            _session = session;
            _paradigm = (MidasTouchParadigm) session.Paradigm;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            _stageProgram = _paradigm.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.ColorScheme[ColorKeys.Background].ToSdColor().ToSdx();
            _foregroundColor = _paradigm.Config.Gui.ColorScheme[ColorKeys.Foreground].ToSdColor().ToSdx();
            _blockBorderColor = _paradigm.Config.Gui.ButtonBorder.Color.ToSdColor().ToSdx();
            _blockNormalColor = _paradigm.Config.Gui.ButtonNormalColor.ToSdColor().ToSdx();
        }

        public new void Show()
        {
            ((Control) this).Show();
            RenderLoop.Run(this, OnRender);
        }

        public new void Dispose()
        {
            lock (_renderContextLock)
                DisposeDirectXResources();
            base.Dispose();
        }

        private void InitializeDirectXResources()
        {
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
                IsWindowed = false
            };

            D3D11.Device.CreateWithSwapChain(D3D.DriverType.Hardware, D3D11.DeviceCreationFlags.BgraSupport,
                new[] { D3D.FeatureLevel.Level_10_0 }, swapChainDesc,
                out _d3DDevice, out var swapChain);
            _d3DDeviceContext = _d3DDevice.ImmediateContext;

            _swapChain = new DXGI.SwapChain1(swapChain.NativePointer);

            _d2DFactory = new D2D1.Factory();

            using (var backBuffer = _swapChain.GetBackBuffer<D3D11.Texture2D>(0))
            {
                _renderTargetView = new D3D11.RenderTargetView(_d3DDevice, backBuffer);
                _renderTarget = new D2D1.RenderTarget(_d2DFactory, backBuffer.QueryInterface<DXGI.Surface>(),
                    new D2D1.RenderTargetProperties(new D2D1.PixelFormat(DXGI.Format.Unknown, D2D1.AlphaMode.Premultiplied)))
                {
                    TextAntialiasMode = D2D1.TextAntialiasMode.Cleartype
                };
            }

            _solidColorBrush = new D2D1.SolidColorBrush(_renderTarget, Color.White);

            _dwFactory = new DW.Factory(DW.FactoryType.Shared);
            _textFormat = new DW.TextFormat(_dwFactory, "Arial", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, 84 * (float)GraphicsUtils.Scale)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
        }

        private void DisposeDirectXResources()
        {
            _textFormat.Dispose();
            _dwFactory.Dispose();
            _renderTarget.Dispose();
            _renderTargetView.Dispose();
            _d2DFactory.Dispose();
            _swapChain.Dispose();
            _d3DDeviceContext.Dispose();
            _d3DDevice.Dispose();
        }

        private void OnRender()
        {
            lock (_renderContextLock)
            {
                if (_renderTarget?.IsDisposed ?? true) return;

                _renderTarget.BeginDraw();
                _renderTarget.Clear(_backgroundColor);

                if (_paradigmStarted && _trialStarted) // Draw blocks
                {
                    var borderRect = SharpDXUtils.CenteredRect(Width / 2f, Height / 2f, _paradigm.Config.Gui.ButtonSize);
                    var buttonRect = borderRect.Shrink(Math.Max((float)_paradigm.Config.Gui.ButtonBorder.Width, 0));
                    var paddings = _paradigm.Config.Gui.ButtonPaddings;
                    var contentRect = buttonRect.Shrink((float)paddings.Left, (float)paddings.Top, (float)paddings.Right, (float)paddings.Bottom);

                    if (_paradigm.Config.Gui.ButtonBorder.Width > 0)
                    {
                        _solidColorBrush.Color = _blockBorderColor;
                        _renderTarget.FillRectangle(borderRect, _solidColorBrush);
                    }

                    _solidColorBrush.Color = _blockNormalColor;
                    _renderTarget.FillRectangle(buttonRect, _solidColorBrush);

                    _solidColorBrush.Color = _foregroundColor;
                    _renderTarget.DrawText(_displayText, _textFormat, contentRect, _solidColorBrush, D2D1.DrawTextOptions.None);
                }
                else if (_displayText?.IsNotBlank() ?? false) // Draw text
                {
                    _solidColorBrush.Color = _foregroundColor;
                    _renderTarget.DrawText(_displayText, _textFormat, new RawRectangleF(0, 0, Width, Height),
                        _solidColorBrush, D2D1.DrawTextOptions.None);
                }

                _renderTarget.EndDraw();

                _swapChain.Present(1, DXGI.PresentFlags.None, _presentParameters);
            }
        }

        private void Window_OnLoaded(object sender, EventArgs e)
        {
            lock (_renderContextLock)
                InitializeDirectXResources();

            _session.Start();
            _stageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            _markable?.Mark(MarkerDefinitions.UserExitMarker);
            Stop(true);
        }

        private void Window_OnResize(object sender, EventArgs e)
        {
            lock (_renderContextLock)
            {
                if (_d3DDeviceContext?.IsDisposed ?? true)
                    return;
                DisposeDirectXResources();
                InitializeDirectXResources();
            }
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
                }
            }

        }

        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        private void Stop(bool userInterrupted = false)
        {
            _swapChain.IsFullScreen = false;
            Dispose();
            Close();
            _stageProgram.Stop();
            _session.Finish(userInterrupted);
        }

    }
}
