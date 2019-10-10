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
using SharpDX.Direct3D9;
using D2D1 = SharpDX.Direct2D1;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.Windows;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using RenderForm = SharpDX.Windows.RenderForm;

namespace SharpBCI.Paradigms.VEP.SSVEP
{

    internal class SsvepExperimentWindow : RenderForm, IDisposable
    {

        private interface IStimulationPresenter
        {

            void Initialize(D2D1.RenderTarget renderTarget, RawVector2 size, Block[] blocks);

            void Destroy();

            void Present(D2D1.RenderTarget renderTarget, Block block, double secsPassed);

        }

        private class Square01StimulationPresenter : IStimulationPresenter
        {

            private readonly Color _normalColor;

            private readonly Color _flashingColor;

            private D2D1.SolidColorBrush _brush;

            public Square01StimulationPresenter(Color normalColor, Color flashingColor)
            {
                _normalColor = normalColor;
                _flashingColor = flashingColor;
            }

            public void Initialize(D2D1.RenderTarget renderTarget, RawVector2 size, Block[] blocks) => _brush = new D2D1.SolidColorBrush(renderTarget, _normalColor);

            public void Destroy() => _brush.Dispose();

            public void Present(D2D1.RenderTarget renderTarget, Block block, double secsPassed)
            {
                _brush.Color = (float)ConvertCosineValueToGrayScale(block.Pattern.Sample(secsPassed)) < 0.5 ? _flashingColor : _normalColor;
                renderTarget.FillRectangle(block.ContentRect, _brush);
            }

        }

        private class SineGradientStimulationPresenter : IStimulationPresenter
        {

            private readonly Color _normalColor;

            private readonly Color _flashingColor;

            private D2D1.SolidColorBrush _brush;

            public SineGradientStimulationPresenter(Color normalColor, Color flashingColor)
            {
                _normalColor = normalColor;
                _flashingColor = flashingColor;
            }

            public void Initialize(D2D1.RenderTarget renderTarget, RawVector2 size, Block[] blocks) => _brush = new D2D1.SolidColorBrush(renderTarget, _normalColor);

            public void Destroy() => _brush.Dispose();

            public void Present(D2D1.RenderTarget renderTarget, Block block, double secsPassed)
            {
                _brush.Color = Color.SmoothStep(_normalColor, _flashingColor, (float)ConvertCosineValueToGrayScale(block.Pattern.Sample(secsPassed)));
                renderTarget.FillRectangle(block.ContentRect, _brush);
            }

        }

        private class SquareCheckerboardStimulationPresenter : IStimulationPresenter
        {

            private readonly Color _normalColor;

            private readonly Color _flashingColor;

            private readonly int _density;

            private D2D1.Bitmap _bitmap0, _bitmap1;

            private D2D1.BitmapBrush _brush;

            public SquareCheckerboardStimulationPresenter(Color normalColor, Color flashingColor)
            {
                _normalColor = normalColor;
                _flashingColor = flashingColor;
                _density = 5;
            }

            private static void DrawCheckerboard(System.Drawing.Bitmap bitmap, System.Drawing.Color color0, System.Drawing.Color color1, int subLen)
            {
                var imgWidth = bitmap.Width;
                var imgHeight = bitmap.Height;
                for (var r = 0; r < imgHeight; r++)
                    for (var c = 0; c < imgWidth; c++)
                        bitmap.SetPixel(c, r, (r / subLen % 2) == (c / subLen % 2) ? color0 : color1);
            }

            public void Initialize(D2D1.RenderTarget renderTarget, RawVector2 size, Block[] blocks)
            {
                var width = (int)Math.Round(size.X);
                var height = (int)Math.Round(size.Y);
                var longerSide = Math.Max(width, height);
                var subLen = longerSide / _density + Math.Sign(longerSide % _density);
                var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                var color0 = _normalColor.ToSd();
                var color1 = _flashingColor.ToSd();
                DrawCheckerboard(bitmap, color0, color1, subLen);
                _bitmap0 = bitmap.ToD2D1Bitmap(renderTarget);
                DrawCheckerboard(bitmap, color1, color0, subLen);
                _bitmap1 = bitmap.ToD2D1Bitmap(renderTarget);
                bitmap.Dispose();
                _brush = new D2D1.BitmapBrush(renderTarget, _bitmap0);
                _brush.ExtendModeX = _brush.ExtendModeY = D2D1.ExtendMode.Clamp;
                _brush.InterpolationMode = D2D1.BitmapInterpolationMode.NearestNeighbor;
                foreach (var block in blocks) block.Tag = Matrix3x2.Translation(block.ContentRect.Left, block.ContentRect.Top);

            }

            public void Destroy()
            {
                _bitmap0?.Dispose();
                _bitmap1?.Dispose();
                _brush?.Dispose();
            }

            public void Present(D2D1.RenderTarget renderTarget, Block block, double secsPassed)
            {
                var bitmap = (float)ConvertCosineValueToGrayScale(block.Pattern.Sample(secsPassed)) < 0.5 ? _bitmap0 : _bitmap1;
                if (bitmap == null) return;
                _brush.Bitmap = bitmap;
                _brush.Transform = (Matrix3x2)block.Tag;
                renderTarget.FillRectangle(block.ContentRect, _brush);
            }

        }

        private class SquareCheckerboardRadicalStimulationPresenter : IStimulationPresenter
        {

            private readonly Color _normalColor;

            private readonly Color _flashingColor;

            private readonly int _density, _densityTheta;

            private D2D1.Bitmap _bitmap0, _bitmap1;

            private D2D1.BitmapBrush _brush;

            public SquareCheckerboardRadicalStimulationPresenter(Color normalColor, Color flashingColor)
            {
                _normalColor = normalColor;
                _flashingColor = flashingColor;
                _density = 8;
                _densityTheta = 16;
            }

            private static void DrawCheckerboard(System.Drawing.Bitmap bitmap, System.Drawing.Color color0, System.Drawing.Color color1, double subLen, double subRad)
            {
                var imgWidth = bitmap.Width;
                var imgHeight = bitmap.Height;
                var centerX = imgWidth / 2.0;
                var centerY = imgHeight / 2.0;
                for (var r = 0; r < imgHeight; r++)
                    for (var c = 0; c < imgWidth; c++)
                    {
                        var dx = centerX - c;
                        var dy = centerY - r;
                        var dist = Math.Sqrt(dx * dx + dy * dy);
                        var rad = Math.Atan2(dy, dx) + Math.PI * 2;
                        var color = (int)Math.Round(dist / subLen) % 2 == (int)Math.Round(rad / subRad) % 2 ? color0 : color1;
                        bitmap.SetPixel(c, r, color);
                    }
            }

            public void Initialize(D2D1.RenderTarget renderTarget, RawVector2 size, Block[] blocks)
            {
                var width = (int)Math.Round(size.X);
                var height = (int)Math.Round(size.Y);
                var subLen = Math.Max(size.X, size.Y) / _density;
                var subRad = Math.PI * 2 / _densityTheta;
                var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                var color0 = _normalColor.ToSd();
                var color1 = _flashingColor.ToSd();
                DrawCheckerboard(bitmap, color0, color1, subLen, subRad);
                _bitmap0 = bitmap.ToD2D1Bitmap(renderTarget);
                DrawCheckerboard(bitmap, color1, color0, subLen, subRad);
                _bitmap1 = bitmap.ToD2D1Bitmap(renderTarget);
                bitmap.Dispose();
                _brush = new D2D1.BitmapBrush(renderTarget, _bitmap0);
                _brush.ExtendModeX = _brush.ExtendModeY = D2D1.ExtendMode.Clamp;
                _brush.InterpolationMode = D2D1.BitmapInterpolationMode.NearestNeighbor;
                foreach (var block in blocks) block.Tag = Matrix3x2.Translation(block.ContentRect.Left, block.ContentRect.Top);

            }

            public void Destroy()
            {
                _bitmap0?.Dispose();
                _bitmap1?.Dispose();
                _brush?.Dispose();
            }

            public void Present(D2D1.RenderTarget renderTarget, Block block, double secsPassed)
            {
                var bitmap = (float)ConvertCosineValueToGrayScale(block.Pattern.Sample(secsPassed)) < 0.5 ? _bitmap0 : _bitmap1;
                if (bitmap == null) return;
                _brush.Bitmap = bitmap;
                _brush.Transform = (Matrix3x2)block.Tag;
                renderTarget.FillRectangle(block.ContentRect, _brush);
            }

        }

        private class Block
        {
            
            public RawVector2 Center;

            public RawVector2 Size;

            public ITemporalPattern Pattern;

            public RawRectangleF? BorderRect;

            public RawRectangleF ContentRect;

            public D2D1.Ellipse? CenterPointEllipse;

            public object Tag;

            public void UpdateGeometries(float borderWidth, float fixationPointSize)
            {
                float halfWidth = Size.X / 2, halfHeight = Size.Y / 2;
                var outerRect = new RawRectangleF
                {
                    Left = Center.X - halfWidth,
                    Right = Center.X + halfWidth,
                    Top = Center.Y - halfHeight,
                    Bottom = Center.Y + halfHeight
                };

                if (borderWidth > 0)
                {
                    BorderRect = outerRect;
                    ContentRect.Left = outerRect.Left + borderWidth;
                    ContentRect.Right = outerRect.Right - borderWidth;
                    ContentRect.Top = outerRect.Top + borderWidth;
                    ContentRect.Bottom = outerRect.Bottom - borderWidth;
                }
                else
                {
                    BorderRect = null;
                    ContentRect = outerRect;
                }
                CenterPointEllipse = fixationPointSize > 0 ? new D2D1.Ellipse(Center, fixationPointSize, fixationPointSize) : (D2D1.Ellipse?)null;
            }

        }

        private readonly object _renderContextLock = new object();

        private readonly Session _session;

        private readonly SsvepParadigm _paradigm;

        private readonly AutoResetEvent _trialStartEvent;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        private readonly Block[] _blocks;

        private readonly IStimulationPresenter _presenter;

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

        private readonly DXGI.PresentParameters _presentParameters = new DXGI.PresentParameters();
        
        private D3D11.Device _d3DDevice;

        private D3D11.DeviceContext _d3DDeviceContext;

        private DXGI.SwapChain1 _swapChain;

        private D3D11.RenderTargetView _renderTargetView;

        private D2D1.Factory _d2DFactory;

        private D2D1.RenderTarget _renderTarget;

        private D2D1.SolidColorBrush _solidColorBrush;

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
            _trialStartEvent = _paradigm.Config.Test.PressKeyToStart.HasValue ? new AutoResetEvent(false) : null; 
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            _stageProgram = _paradigm.CreateStagedProgram(session, _trialStartEvent);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            _blocks = new Block[(int)_paradigm.Config.Gui.BlockLayout.Volume];
            for (var i = 0; i < _blocks.Length; i++)
            {
                var block = _blocks[i] = new Block();
                block.Size = new RawVector2(_paradigm.Config.Gui.BlockSize.Width, _paradigm.Config.Gui.BlockSize.Height);
                block.Pattern = _paradigm.Config.Test.Patterns[i];
            }

            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            _fontColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().Inverted().ToSdx();
            _blockBorderColor = _paradigm.Config.Gui.BlockBorder.Color.ToSdColor().ToSdx();
            _blockNormalColor = _paradigm.Config.Gui.BlockColors[0].ToSdColor().ToSdx();
            _blockFlashingColor = _paradigm.Config.Gui.BlockColors[1].ToSdColor().ToSdx();
            _blockFixationPointColor = _paradigm.Config.Gui.BlockFixationPoint.Color.ToSdColor().ToSdx();

            /* Initialize presenter */
            switch (_paradigm.Config.Test.StimulationType)
            {
                case SsvepStimulationType.Square01:
                    _presenter = new Square01StimulationPresenter(_blockNormalColor, _blockFlashingColor);
                    break;
                case SsvepStimulationType.SineGradient:
                    _presenter = new SineGradientStimulationPresenter(_blockNormalColor, _blockFlashingColor);
                    break;
                case SsvepStimulationType.SquareCheckerboard:
                    _presenter = new SquareCheckerboardStimulationPresenter(_blockNormalColor, _blockFlashingColor);
                    break;
                case SsvepStimulationType.SquareCheckerboardRadical:
                    _presenter = new SquareCheckerboardRadicalStimulationPresenter(_blockNormalColor, _blockFlashingColor);
                    break;
                default:
                    throw new NotSupportedException(_paradigm.Config.Test.StimulationType.ToString());
            }
        }

        private static double ConvertCosineValueToGrayScale(double cosVal) => (-cosVal + 1) / 2;

        public new void Show()
        {
            ((Control) this).Show();
            RenderLoop.Run(this, OnRender);
        }

        public new void Dispose()
        {
            _presenter.Destroy();
            lock (_renderContextLock)
                DisposeDirectXResources();
            base.Dispose();
        }

        private void UpdateResources()
        {
            lock (_renderContextLock)
                InitializeDirectXResources();
            var guiScale = (float)GraphicsUtils.Scale;
            var borderWidth = Math.Max(0, (float)_paradigm.Config.Gui.BlockBorder.Width * guiScale);
            var fixationPointSize = _paradigm.Config.Gui.BlockFixationPoint.Size * guiScale;
            var blockSize = UpdateBlocks(borderWidth, fixationPointSize);
            lock (_renderContextLock)
                _presenter.Initialize(_renderTarget, new RawVector2(blockSize.X - borderWidth * 2, blockSize.Y - borderWidth * 2), _blocks);
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
                IsWindowed = _paradigm.Config.Test.Debug
            };

            D3D11.Device.CreateWithSwapChain(SharpDX.Direct3D.DriverType.Hardware, D3D11.DeviceCreationFlags.BgraSupport,
                new[] { SharpDX.Direct3D.FeatureLevel.Level_10_0 }, swapChainDesc,
                out _d3DDevice, out var swapChain);
            _d3DDeviceContext = _d3DDevice.ImmediateContext;
            
            _swapChain = new DXGI.SwapChain1(swapChain.NativePointer);
// TODO            _swapChain.ResizeTarget();

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

            _dwFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);
            _textFormat = new SharpDX.DirectWrite.TextFormat(_dwFactory, "Arial", SharpDX.DirectWrite.FontWeight.Bold,
                SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, 84 * (float)GraphicsUtils.Scale)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center
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

        private RawVector2 UpdateBlocks(float borderWidth, float fixationPointSize)
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

            var blockSize = new RawVector2(blockWidth, blockHeight);
            for (var i = 0; i < _blocks.Length; i++)
            {
                var block = _blocks[i];
                var row = i / layout[1];
                var col = i % layout[1];

                block.Center.X = offsetX + blockFrameW * col + blockFrameW / 2F;
                block.Center.Y = offsetY + blockFrameH * row + blockFrameH / 2F;

                block.Size = blockSize;
                block.UpdateGeometries(borderWidth, fixationPointSize);
            }
            return blockSize;
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
                    foreach (var block in _blocks)
                    {
                        if (block.BorderRect != null)
                        {
                            _solidColorBrush.Color = _blockBorderColor;
                            _renderTarget.FillRectangle(block.BorderRect.Value, _solidColorBrush);
                        }
                        if (!_trialStarted || block.Pattern == null)
                        {
                            _solidColorBrush.Color = _blockNormalColor;
                            _renderTarget.FillRectangle(block.ContentRect, _solidColorBrush);
                        }
                        else
                            _presenter.Present(_renderTarget, block, secsPassed);

                        if (block.CenterPointEllipse != null)
                        {
                            _solidColorBrush.Color = _blockFixationPointColor;
                            _renderTarget.FillEllipse(block.CenterPointEllipse.Value, _solidColorBrush);
                        }
                    }
                }
                else if (!(_displayText?.IsBlank() ?? true)) // Draw text
                {
                    _solidColorBrush.Color = _fontColor;
                    _renderTarget.DrawText(_displayText, _textFormat, new RawRectangleF(0, 0, Width, Height),
                        _solidColorBrush, D2D1.DrawTextOptions.None);
                }

                _renderTarget.EndDraw();

                _swapChain.Present(1, DXGI.PresentFlags.None, _presentParameters);
            }
        }

        private void Window_OnLoaded(object sender, EventArgs e)
        {
            UpdateResources();

            _session.Start();
            _stageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (_trialStartEvent != null && e.KeyCode == _paradigm.Config.Test.PressKeyToStart.Value)
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
                if (_d3DDeviceContext?.IsDisposed ?? true) return;
                DisposeDirectXResources();
            }
            UpdateResources();
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
