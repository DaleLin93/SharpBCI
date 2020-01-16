using System;
using System.Diagnostics.CodeAnalysis;
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
using D2D1 = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace SharpBCI.Paradigms.VEP.SSVEP
{

    internal class SsvepExperimentWindow : SimpleD2DForm
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

            public string Text;

            public RawRectangleF? BorderRect;

            public RawRectangleF ContentRect;

            public D2D1.Ellipse? FixationEllipse;

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
                FixationEllipse = fixationPointSize > 0 ? new D2D1.Ellipse(Center, fixationPointSize, fixationPointSize) : (D2D1.Ellipse?)null;
            }

        }

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

        private readonly Color _blockFontColor;

        private readonly Color _blockFixationPointColor;

        /* D3D Resources */

        private DW.TextFormat _blockTextFormat;

        private DW.TextFormat _cueTextFormat;

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
            this.HideCursorInside();

            _session = session;
            _paradigm = (SsvepParadigm) session.Paradigm;
            _trialStartEvent = _paradigm.Config.Test.PressKeyToStartBlock.HasValue ? new AutoResetEvent(false) : null; 
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            _stageProgram = _paradigm.CreateStagedProgram(session, _trialStartEvent);
            _stageProgram.StageChanged += StageProgram_StageChanged;

            _blocks = new Block[(int)_paradigm.Config.Gui.BlockLayout.Volume];
            for (var i = 0; i < _blocks.Length; i++)
            {
                var block = _blocks[i] = new Block();
                block.Size = new RawVector2(_paradigm.Config.Gui.BlockSize.Width, _paradigm.Config.Gui.BlockSize.Height);
                block.Pattern = _paradigm.Config.Test.Patterns[i];
                block.Text = _paradigm.Config.Gui.GetBlockText(i);
            }

            /* Type conversion */
            _backgroundColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().ToSdx();
            _fontColor = _paradigm.Config.Gui.BackgroundColor.ToSdColor().Inverted().ToSdx();
            _blockBorderColor = _paradigm.Config.Gui.BlockBorder.Color.ToSdColor().ToSdx();
            _blockNormalColor = _paradigm.Config.Gui.BlockColors[0].ToSdColor().ToSdx();
            _blockFlashingColor = _paradigm.Config.Gui.BlockColors[1].ToSdColor().ToSdx();
            _blockFontColor = _paradigm.Config.Gui.BlockFontColor.ToSdColor().ToSdx();
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
        
        protected override void Draw(D2D1.RenderTarget renderTarget)
        {
            /* Clear canvas */
            renderTarget.Clear(_backgroundColor);

            /* Draw blocks */
            if (_paradigmStarted) 
            {
                var secsPassed = (CurrentTime - _stageUpdatedAt) / 1000.0;
                foreach (var block in _blocks)
                {
                    /* Draw block border */
                    if (block.BorderRect != null)
                    {
                        SolidColorBrush.Color = _blockBorderColor;
                        renderTarget.FillRectangle(block.BorderRect.Value, SolidColorBrush);
                    }

                    /* Fill block content */
                    if (!_trialStarted || block.Pattern == null)
                    {
                        SolidColorBrush.Color = _blockNormalColor;
                        renderTarget.FillRectangle(block.ContentRect, SolidColorBrush);
                    }
                    else
                        _presenter.Present(renderTarget, block, secsPassed);

                    /* Draw block text */
                    if (block.Text != null)
                    {
                        SolidColorBrush.Color = _blockFontColor;
                        renderTarget.DrawText(block.Text, _blockTextFormat, block.ContentRect,
                            SolidColorBrush, D2D1.DrawTextOptions.None);
                    }

                    /* Draw block fixation */
                    if (block.FixationEllipse != null)
                    {
                        SolidColorBrush.Color = _blockFixationPointColor;
                        renderTarget.FillEllipse(block.FixationEllipse.Value, SolidColorBrush);
                    }
                }
            }
            else if (_displayText != null) // Draw text
            {
                SolidColorBrush.Color = _fontColor;
                renderTarget.DrawText(_displayText, _cueTextFormat, new RawRectangleF(0, 0, Width, Height),
                    SolidColorBrush, D2D1.DrawTextOptions.None);
            }
        }

        protected override void InitializeDirectXResources()
        {
            base.InitializeDirectXResources();
            var guiScale = (float) GraphicsUtils.Scale;
            _blockTextFormat = new DW.TextFormat(DwFactory, "Arial", _paradigm.Config.Gui.BlockFontSize * guiScale)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            _cueTextFormat = new DW.TextFormat(DwFactory, "Arial", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, 84 * guiScale)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
            UpdateResources();
        }

        protected override void ResizeRenderTarget()
        {
            base.ResizeRenderTarget();
            UpdateResources();
        }

        protected override void DisposeDirectXResources()
        {
            _presenter.Destroy();
            _blockTextFormat.Dispose();
            _cueTextFormat.Dispose();
            base.DisposeDirectXResources();
        }

        private void UpdateResources()
        {
            var guiScale = (float)GraphicsUtils.Scale;
            var borderWidth = Math.Max(0, (float)_paradigm.Config.Gui.BlockBorder.Width * guiScale);
            var fixationPointSize = _paradigm.Config.Gui.BlockFixationPoint.Size * guiScale;
            var blockSize = UpdateBlocks(borderWidth, fixationPointSize);
            _presenter.Initialize(RenderTarget, new RawVector2(blockSize.X - borderWidth * 2, blockSize.Y - borderWidth * 2), _blocks);
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

        private void Window_OnLoaded(object sender, EventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
        }

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (_trialStartEvent != null && e.KeyCode == _paradigm.Config.Test.PressKeyToStartBlock.Value)
            {
                _trialStartEvent.Set();
                return;
            }
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

            _stageUpdatedAt = CurrentTime;
            var stage = e.Stage;

            _displayText = stage.Cue?.Trim2Null();

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
            Dispose();
            Close();
            _stageProgram.Stop();
            _session.Finish(userInterrupted);
        }

        private ulong CurrentTime => _session.SessionTime;

    }
}
