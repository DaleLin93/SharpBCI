using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MarukoLib.UI;
using SharpBCI.Extensions;
using Image = System.Windows.Controls.Image;

namespace SharpBCI.Experiments.MI
{

    [AppEntry(false)]
    public class MediaTestEntry : IAppEntry
    {

        public string Name => "Media Test";

        public void Run() => new MediaTestWindow().ShowDialog();

    }

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal partial class MediaTestWindow
    {

        private DirectShowVideoSource _videoSource0, _videoSource1;

        public MediaTestWindow() => InitializeComponent();

        private void MediaTestWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                _videoSource0 = new DirectShowVideoSource(new Uri("file://d:/A.mp4"), true);
                _videoSource0.NewFrame += (s0, e0) => ImageA.DispatcherInvoke(img => { img.Source = ((Bitmap) e0.Clone()).ToBitmapSource(); });
                _videoSource0.Play();
                _videoSource1 = new DirectShowVideoSource(new Uri("file://d:/B.mp4"), true);
                _videoSource1.NewFrame += (s0, e0) => ImageB.DispatcherInvoke(img => { img.Source = ((Bitmap) e0.Clone()).ToBitmapSource(); });
                _videoSource1.Play();

                Thread.Sleep(10000);

                _videoSource0.Pause();
                _videoSource0.Rewind();
                _videoSource0.Play();

                _videoSource1.Pause();
                _videoSource1.Rewind();
                _videoSource1.Play();
            }).Start();
        }

        private void MediaTestWindow_OnClosed(object sender, EventArgs e)
        {
        }
        
        public IntPtr Handle => new WindowInteropHelper(this).Handle;

        private void MediaTestWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Image image;
                DirectShowVideoSource videoSource;
                switch (e.ChangedButton)
                {
                    case MouseButton.Left:
                        image = ImageA;
                        videoSource = _videoSource0;
                        break;
                    case MouseButton.Right:
                        image = ImageB;
                        videoSource = _videoSource1;
                        break;
                    default:
                        image = null;
                        videoSource = null;
                        break;
                }

                if (videoSource == null) return;

                switch (e.ClickCount)
                {
                    case 1:
                        lock (this)
                        {
                            videoSource.Pause();
                            videoSource.Rewind();
                            videoSource.Play();
                        }
                        break;
                    case 2:
                        image.Visibility = image.IsVisible ? Visibility.Hidden : Visibility.Visible;
                        break;
                }

            });
        }

        private void MediaTestWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

    }

}
