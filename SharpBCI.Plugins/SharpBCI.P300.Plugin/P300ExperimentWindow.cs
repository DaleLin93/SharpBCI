using SharpBCI.Core.Staging;
using SharpDX.Windows;
using System;
using System.Windows.Forms;
using MarukoLib.Lang;
using DW = SharpDX.DirectWrite;
using DXGI = SharpDX.DXGI;
using D2D1 = SharpDX.Direct2D1;
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using SharpBCI.Core.IO;
using System.Collections.Generic;
using MarukoLib.DirectX;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using Color = SharpDX.Color;
using RenderForm = SharpDX.Windows.RenderForm;

namespace SharpBCI.Paradigms.P300
{

    internal class P300ExperimentWindow : RenderForm, IDisposable
    {

        private class Block
        {

            public bool Target;

            public IRandomBoolSequence Random;
            
            public RawVector2 Center;

            public RawVector2 Size;

            public float BorderWidth;

            public bool Actived;

            public RawRectangleF BorderRect;

            public RawRectangleF ContentRect;

            public D2D1.Ellipse CenterPointEllipse;

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

                var radius = Math.Min(Size.X, Size.Y) / 100;
                CenterPointEllipse = new D2D1.Ellipse(Center, radius, radius);
            }

        }

        private readonly object _renderContextLock = new object();

        private readonly Session _session;

        private readonly P300Paradigm _paradigm;

        private readonly IMarkable _markable;

        private readonly Block[] _blocks;

        private readonly StageProgram _stageProgram;

        private readonly P300Paradigm.Result _result;

        /* Paradigm variables */

        private bool _paradigmStarted = false;

        private string _displayText = null;

        private P300Paradigm.Result.Trial _trial;

        /* Converted variables */

        private readonly Color _backgroundColor;

        private readonly Color _fontColor;

        private readonly Color _blockBorderColor;

        private readonly Color _blockNormalColor;

        private readonly Color _blockActivedColor;

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

        private D2D1.Bitmap _bitmap;

        public P300ExperimentWindow(Session session)
        {
            // ReSharper disable once LocalizableElement
            Text = "P300";
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
            _paradigm = (P300Paradigm) session.Paradigm;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();
            _result = new P300Paradigm.Result { Trials = new LinkedList<P300Paradigm.Result.Trial>() };

            _stageProgram = _paradigm.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            /* Initialize blocks */
            _blocks = new Block[(int)_paradigm.Config.Test.Layout];
            for (var i = 0; i < _blocks.Length; i++)
            {
                var block = new Block
                {
                    Target = _blocks.Length / 2 == i,
                    Random = _paradigm.Config.Test.TargetRate
                    .CreateRandomBoolSequence((int)(DateTimeUtils.CurrentTimeTicks << 1 + i)),
                    Size = new RawVector2(_paradigm.Config.Gui.BlockLayout.Width, _paradigm.Config.Gui.BlockLayout.Height),
                    BorderWidth = (float)_paradigm.Config.Gui.BlockBorder.Width
                };
                block.UpdateGeometries();
                _blocks[i] = block;
            }
            
            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            _fontColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().Inverted().ToSdx();
            _blockBorderColor = _paradigm.Config.Gui.BlockBorder.Color.ToSdColor().ToSdx();
            _blockNormalColor = _paradigm.Config.Gui.BlockNormalColor.ToSdColor().ToSdx();
            _blockActivedColor = _paradigm.Config.Gui.BlockActivedColor.ToSdColor().ToSdx();
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
                new[] { D3D.FeatureLevel.Level_10_0 }, swapChainDesc, out _d3DDevice, out var swapChain);
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

            _bitmap = _paradigm.Config.Gui.UseBitmap ? Properties.Resources.Einstein.ToD2D1Bitmap(_renderTarget) : null;
        }

        private void DisposeDirectXResources()
        {
            _bitmap?.Dispose();
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
            var layoutSize = _paradigm.Config.Test.Layout.GetLayoutSize();
            var position = _paradigm.Config.Gui.BlockPosition;
            float winW = Width, winH = Height;
            float blockFrameW, blockFrameH;
            float offsetX = 0, offsetY = 0;

            var blockWidth = (int)(_paradigm.Config.Gui.BlockLayout.Width * scaleFactor);
            var blockMarginH = (int)(_paradigm.Config.Gui.BlockLayout.HMargin * scaleFactor);
            if (blockWidth <= 0 || blockMarginH <= 0)
            {
                blockFrameW = winW / layoutSize[1];
                blockWidth = blockWidth <= 0 ? (int)(blockFrameW - blockMarginH) : blockWidth;
            }
            else
            {
                blockFrameW = blockWidth + blockMarginH;
                var wSum = blockFrameW * layoutSize[1];
                if (wSum > winW)
                {
                    blockFrameW *= winW / wSum;
                    wSum = winW;
                }
                offsetX = (winW - wSum) * position.GetHorizontalPosition().ToPosition1D().GetPositionValue();
            }

            var blockHeight = (int)(_paradigm.Config.Gui.BlockLayout.Height * scaleFactor);
            var blockMarginV = (int)(_paradigm.Config.Gui.BlockLayout.VMargin * scaleFactor);
            if (blockHeight <= 0 || blockMarginV <= 0)
            {
                blockFrameH = winH / layoutSize[0];
                blockHeight = blockHeight <= 0 ? (int)(blockFrameH - blockMarginH) : blockHeight;
            }
            else
            {
                blockFrameH = blockHeight + blockMarginV;
                var hSum = blockFrameH * layoutSize[0];
                if (hSum > winH)
                {
                    blockFrameH *= winH / hSum;
                    hSum = winH;
                }
                offsetY = (winH - hSum) * position.GetVerticalPosition().ToPosition1D().GetPositionValue();
            }

            for (var i = 0; i < _blocks.Length; i++)
            {
                var row = i / layoutSize[1];
                var col = i % layoutSize[1];

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
                if (_renderTarget?.IsDisposed ?? true)
                    return;

                _renderTarget.BeginDraw();
                _renderTarget.Clear(_backgroundColor);

                if (_paradigmStarted) // Draw blocks
                {
                    foreach (var block in _blocks)
                    {
                        if (block.BorderWidth > 0)
                        {
                            _solidColorBrush.Color = _blockBorderColor;
                            _renderTarget.FillRectangle(block.BorderRect, _solidColorBrush);
                        }

                        if (_bitmap != null)
                        {
                            _solidColorBrush.Color = _blockNormalColor;
                            _renderTarget.FillRectangle(block.ContentRect, _solidColorBrush);
                            if (block.Actived)
                                _renderTarget.DrawBitmap(_bitmap, block.ContentRect, 1, D2D1.BitmapInterpolationMode.Linear);
                        }
                        else
                        {
                            _solidColorBrush.Color = block.Actived ? _blockActivedColor : _blockNormalColor;
                            _renderTarget.FillRectangle(block.ContentRect, _solidColorBrush);
                        }

                        if (block.Target)
                        {
                            _solidColorBrush.Color = _blockBorderColor;
                            _renderTarget.FillEllipse(block.CenterPointEllipse, _solidColorBrush);
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
            lock (_renderContextLock)
                InitializeDirectXResources();
            UpdateBlocks();

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
                if (_d3DDeviceContext?.IsDisposed ?? true) return;
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

            var stage = e.Stage;

            if (stage.Marker != null)
            {
                _markable?.Mark(stage.Marker.Value);

                switch (e.Stage.Marker)
                {
                    case MarkerDefinitions.ParadigmStartMarker:
                        _paradigmStarted = true;
                        break;
                    case MarkerDefinitions.ParadigmEndMarker:
                        _paradigmStarted = false;
                        break;
                    case MarkerDefinitions.TrialStartMarker:
                    {
                        var trial = new P300Paradigm.Result.Trial();
                        _result.Trials.Add(trial);
                        trial.SubTrials = new List<P300Paradigm.Result.Trial.SubTrial>((int)(_paradigm.Config.Test.SubTrialCount + 1));
                        trial.Timestamp = CurrentTime;
                        _trial = trial;
                        _displayText = null;
                        break;
                    }
                    case MarkerDefinitions.TrialEndMarker:
                        _trial = null;
                        break;
                    case P300Paradigm.SubTrialMarker:
                        var targetActived = false;
                        var flags = new bool[_blocks.Length];
                        for (var i = 0; i < _blocks.Length; i++)
                        {
                            var block = _blocks[i];
                            var flag = flags[i] = block.Random.Next();
                            block.Actived = flag;
                            if (flag && block.Target) targetActived = true;
                        }
                        if (targetActived)
                            _markable?.Mark(P300Paradigm.OddBallEventMarker);
                        _trial.SubTrials.Add(new P300Paradigm.Result.Trial.SubTrial { Timestamp = CurrentTime, Flags = flags });
                        break;
                }
            }

            _displayText = stage.Cue;
        }

        private void Stop(bool userInterrupted = false)
        {
            _swapChain.IsFullScreen = false;
            Dispose();
            Close();
            _stageProgram.Stop();
            _session.Finish(_result, userInterrupted);
        }

        private ulong CurrentTime => _session.SessionTime;

    }
}
