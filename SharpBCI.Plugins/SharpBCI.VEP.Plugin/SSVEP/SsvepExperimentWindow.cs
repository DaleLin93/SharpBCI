using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MarukoLib.DirectX;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Patterns;
using SharpDX;
using SharpDX.Mathematics.Interop;
using SharpDX.Windows;
using RenderForm = SharpDX.Windows.RenderForm;

namespace SharpBCI.Paradigms.VEP.SSVEP
{

    internal class SsvepExperimentWindow : RenderForm, IDisposable
    {

        private struct Block
        {
            
            public RawVector2 Center;

            public RawVector2 Size;

            public float BorderWidth;

            public float FixationPointSize;

            public ITemporalPattern[] Patterns;

            public RawRectangleF BorderRect;

            public RawRectangleF ContentRect;

            public RawRectangleF[] DualFlickerRects;

            public SharpDX.Direct2D1.Ellipse CenterPointEllipse;

            public void UpdateGeometries()
            {
                float halfWidth = Size.X / 2, halfHeight = Size.Y / 2;
                BorderRect.Left = Center.X - halfWidth;
                BorderRect.Right = Center.X + halfWidth;
                BorderRect.Top = Center.Y - halfHeight;
                BorderRect.Bottom = Center.Y + halfHeight;

                if (BorderWidth > 0)
                {
                    ContentRect.Left = BorderRect.Left + BorderWidth;
                    ContentRect.Right = BorderRect.Right - BorderWidth;
                    ContentRect.Top = BorderRect.Top + BorderWidth;
                    ContentRect.Bottom = BorderRect.Bottom - BorderWidth;
                }
                else
                    ContentRect = BorderRect;

                DualFlickerRects = new []
                {
                    new RawRectangleF(ContentRect.Left, ContentRect.Top, ContentRect.Left + ContentRect.Width() / 2, ContentRect.Bottom),
                    new RawRectangleF(ContentRect.Left + ContentRect.Width() / 2, ContentRect.Top, ContentRect.Right, ContentRect.Bottom),
                };

                CenterPointEllipse = new SharpDX.Direct2D1.Ellipse(Center, FixationPointSize, FixationPointSize);
            }

        }

        private readonly object _renderContextLock = new object();

        private readonly Session _session;

        private readonly SsvepParadigm _paradigm;

        private readonly AutoResetEvent _trialStartEvent;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        private readonly SsvepParadigm.Configuration.TestConfig.StimulationParadigm _stimParadigm;

        private readonly Block[] _blocks;

        /* Paradigm variables */

        private bool _paradigmStarted = false;

        private bool _trialStarted = false;

        private string _displayText = null;

        private ulong _stageUpdatedAt;

        /* Converted variables */

        private readonly Color _backgroundColor;

        private readonly Color _fontColor;

        private readonly Color _blockBorderColor;

        private readonly Color _blockNormalColor;

        private readonly Color _blockFlashingColor;

        private readonly Color _blockFixationPointColor;

        /* D3D Resources */

        private readonly SharpDX.DXGI.PresentParameters _presentParameters = new SharpDX.DXGI.PresentParameters();
        
        private SharpDX.Direct3D11.Device _d3DDevice;

        private SharpDX.Direct3D11.DeviceContext _d3DDeviceContext;

        private SharpDX.DXGI.SwapChain1 _swapChain;

        private SharpDX.Direct3D11.RenderTargetView _renderTargetView;

        private SharpDX.Direct2D1.Factory _d2DFactory;

        private SharpDX.Direct2D1.RenderTarget _renderTarget;

        private SharpDX.Direct2D1.SolidColorBrush _solidColorBrush;

        private SharpDX.DirectWrite.Factory _dwFactory;

        private SharpDX.DirectWrite.TextFormat _textFormat;

        public SsvepExperimentWindow(Session session)
        {
            // ReSharper disable once LocalizableElement
            Text = "SSVEP";
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
            _paradigm = (SsvepParadigm) session.Paradigm;
            _trialStartEvent = _paradigm.Config.Test.WaitKeyForTrial ? new AutoResetEvent(false) : null; 
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            _stimParadigm = _paradigm.Config.Test.Paradigm;
            _blocks = new Block[(int)_paradigm.Config.Gui.BlockLayout.Volume];
            var patternMultiplier = _stimParadigm.GetParadigmPatternMultiplier();
            for (var i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].Size = new RawVector2(_paradigm.Config.Gui.BlockSize.Width, _paradigm.Config.Gui.BlockSize.Height);
                _blocks[i].BorderWidth = (float)_paradigm.Config.Gui.BlockBorder.Width * (float)GraphicsUtils.Scale;
                _blocks[i].FixationPointSize = _paradigm.Config.Gui.BlockFixationPoint.Size * (float) GraphicsUtils.Scale;
                var patterns = new ITemporalPattern[patternMultiplier];
                for (var j = 0; j < patternMultiplier; j++)
                    patterns[j] = _paradigm.Config.Test.Patterns[i * patternMultiplier + j];
                _blocks[i].Patterns = patterns.All(Functions.IsNull) ? null : patterns;
                _blocks[i].UpdateGeometries();
            }

            _stageProgram = _paradigm.CreateStagedProgram(session, _trialStartEvent);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            _fontColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().Inverted().ToSdx();
            _blockBorderColor = _paradigm.Config.Gui.BlockBorder.Color.ToSdColor().ToSdx();
            _blockNormalColor = _paradigm.Config.Gui.BlockColors[0].ToSdColor().ToSdx();
            _blockFlashingColor = _paradigm.Config.Gui.BlockColors[1].ToSdColor().ToSdx();
            _blockFixationPointColor = _paradigm.Config.Gui.BlockFixationPoint.Color.ToSdColor().ToSdx();
        }

        private static double ConvertCosineValueToGrayScale(double cosVal) => (-cosVal + 1) / 2;

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
            var backBufferDesc = new SharpDX.DXGI.ModeDescription(clientSize.Width, clientSize.Height,
                new SharpDX.DXGI.Rational(60, 1), SharpDX.DXGI.Format.R8G8B8A8_UNorm);

            var swapChainDesc = new SharpDX.DXGI.SwapChainDescription()
            {
                ModeDescription = backBufferDesc,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = Handle,
                SwapEffect = SharpDX.DXGI.SwapEffect.Discard,
                IsWindowed = false
            };

            SharpDX.Direct3D11.Device.CreateWithSwapChain(SharpDX.Direct3D.DriverType.Hardware, SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport,
                new[] { SharpDX.Direct3D.FeatureLevel.Level_10_0 }, swapChainDesc,
                out _d3DDevice, out var swapChain);
            _d3DDeviceContext = _d3DDevice.ImmediateContext;

            _swapChain = new SharpDX.DXGI.SwapChain1(swapChain.NativePointer);

            _d2DFactory = new SharpDX.Direct2D1.Factory();

            using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
            {
                _renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3DDevice, backBuffer);
                _renderTarget = new SharpDX.Direct2D1.RenderTarget(_d2DFactory, backBuffer.QueryInterface<SharpDX.DXGI.Surface>(),
                    new SharpDX.Direct2D1.RenderTargetProperties(new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied)))
                {
                    TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Cleartype
                };
            }

            _solidColorBrush = new SharpDX.Direct2D1.SolidColorBrush(_renderTarget, Color.White);

            _dwFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);
            _textFormat = new SharpDX.DirectWrite.TextFormat(_dwFactory, "Arial", SharpDX.DirectWrite.FontWeight.Bold,
                SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, 84 * (float)GraphicsUtils.Scale)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center
            };
            //                var rectangleGeometry = new D2D1.RoundedRectangleGeometry(_d2DFactory, 
            //                    new D2D1.RoundedRectangle() { RadiusX = 32, RadiusY = 32, Rect = new RectangleF(128, 128, Width - 128 * 2, Height - 128 * 2) });
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

        private void UpdateBlocks()
        {
            var scaleFactor = (float)GraphicsUtils.Scale;
            var layout = _paradigm.Config.Gui.BlockLayout;
            var position = _paradigm.Config.Gui.BlockPosition;
            float winW = Width, winH = Height;
            float blockFrameW, blockFrameH;
            float offsetX = 0, offsetY = 0;

            var blockWidth = (int)(_paradigm.Config.Gui.BlockSize.Width * scaleFactor);
            var blockMarginH = (int)(_paradigm.Config.Gui.BlockSize.HMargin * scaleFactor);
            if (blockWidth <= 0 || blockMarginH <= 0)
            {
                blockFrameW = winW / layout[1];
                blockWidth = blockWidth <= 0 ? (int) (blockFrameW - blockMarginH) : blockWidth;
            }
            else
            {
                blockFrameW = blockWidth + blockMarginH;
                var wSum = blockFrameW * layout[1];
                if (wSum > winW)
                {
                    blockFrameW *= winW / wSum;
                    wSum = winW;
                }
                offsetX = (winW - wSum) * position.GetHorizontalPosition().ToPosition1D().GetPositionValue();
            }

            var blockHeight = (int)(_paradigm.Config.Gui.BlockSize.Height * scaleFactor);
            var blockMarginV = (int)(_paradigm.Config.Gui.BlockSize.VMargin * scaleFactor);
            if (blockHeight <= 0 || blockMarginV <= 0)
            {
                blockFrameH = winH / layout[0];
                blockHeight = blockHeight <= 0 ? (int)(blockFrameH - blockMarginH) : blockHeight;
            }
            else
            {
                blockFrameH = blockHeight + blockMarginV;
                var hSum = blockFrameH * layout[0];
                if (hSum > winH)
                {
                    blockFrameH *= winH / hSum;
                    hSum = winH;
                }
                offsetY = (winH - hSum) * position.GetVerticalPosition().ToPosition1D().GetPositionValue();
            }

            for (var i = 0; i < _blocks.Length; i++)
            {
                var row = i / layout[1];
                var col = i % layout[1];

                _blocks[i].Center.X = offsetX + blockFrameW * col + blockFrameW / 2F;
                _blocks[i].Center.Y = offsetY + blockFrameH * row + blockFrameH / 2F;

                _blocks[i].Size = new RawVector2(blockWidth, blockHeight);
                _blocks[i].UpdateGeometries();
            }
        }

        private void OnRender()
        {
            lock (_renderContextLock)
            {
                if (_renderTarget?.IsDisposed ?? true) return;

                _renderTarget.BeginDraw();
                _renderTarget.Clear(_backgroundColor);

                if (_paradigmStarted) // Draw blocks
                {
                    var secsPassed = (CurrentTime - _stageUpdatedAt) / 1000.0;
                    switch (_stimParadigm)
                    {
                        case SsvepParadigm.Configuration.TestConfig.StimulationParadigm.Flicker:
                            foreach (var block in _blocks)
                            {
                                if (block.BorderWidth > 0)
                                {
                                    _solidColorBrush.Color = _blockBorderColor;
                                    _renderTarget.FillRectangle(block.BorderRect, _solidColorBrush);
                                }
                                if (!_trialStarted || block.Patterns == null)
                                    _solidColorBrush.Color = _blockNormalColor;
                                else
                                    _solidColorBrush.Color = Color.SmoothStep(_blockNormalColor, _blockFlashingColor,
                                        (float) ConvertCosineValueToGrayScale(block.Patterns[0].Sample(secsPassed)));
                                _renderTarget.FillRectangle(block.ContentRect, _solidColorBrush);

                                if (block.FixationPointSize > 0)
                                {
                                    _solidColorBrush.Color = _blockFixationPointColor;
                                    _renderTarget.FillEllipse(block.CenterPointEllipse, _solidColorBrush);
                                }
                            }
                            break;
                        case SsvepParadigm.Configuration.TestConfig.StimulationParadigm.DualFlickers:
                            foreach (var block in _blocks)
                            {
                                if (block.BorderWidth > 0)
                                {
                                    _solidColorBrush.Color = _blockBorderColor;
                                    _renderTarget.FillRectangle(block.BorderRect, _solidColorBrush);
                                }

                                for (var i = 0; i < block.DualFlickerRects.Length; i++)
                                {
                                    if (!_trialStarted || block.Patterns == null)
                                        _solidColorBrush.Color = _blockNormalColor;
                                    else
                                        _solidColorBrush.Color = Color.SmoothStep(_blockNormalColor, _blockFlashingColor,
                                            (float)ConvertCosineValueToGrayScale(block.Patterns[i].Sample(secsPassed)));
                                    _renderTarget.FillRectangle(block.DualFlickerRects[i], _solidColorBrush);
                                }

                                if (block.FixationPointSize > 0)
                                {
                                    _solidColorBrush.Color = _blockFixationPointColor;
                                    _renderTarget.FillEllipse(block.CenterPointEllipse, _solidColorBrush);
                                }
                            }
                            break;
                    }
                }
                else if (!(_displayText?.IsBlank() ?? true)) // Draw text
                {
                    _solidColorBrush.Color = _fontColor;
                    _renderTarget.DrawText(_displayText, _textFormat, new RawRectangleF(0, 0, Width, Height),
                        _solidColorBrush, SharpDX.Direct2D1.DrawTextOptions.None);
                }

                _renderTarget.EndDraw();

                _swapChain.Present(1, SharpDX.DXGI.PresentFlags.None, _presentParameters);
            }
        }

        private void Window_OnLoaded(object sender, EventArgs e)
        {
            lock (_renderContextLock)
                InitializeDirectXResources();
            UpdateBlocks();

            _session.Start();
            _stageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (_trialStartEvent != null && e.KeyCode == Keys.S)
            {
                _trialStartEvent.Set();
                return;
            }
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
            UpdateBlocks();
        }

        private void StageProgram_StageChanged(object sender, StageChangedEventArgs e)
        {
            /* Get next stage, exit on null (END REACHED) */
            if (e.IsEndReached)
            {
                this.ControlInvoke(self => Stop());
                return;
            }

            _stageUpdatedAt = CurrentTime;
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

        private ulong CurrentTime => _session.SessionTime;

    }
}
