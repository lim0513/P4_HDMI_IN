using AForge.Video.DirectShow;
using AForge.Video;
using NAudio.Wave;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HDMI_IN
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private VideoCaptureDevice videoSource;    // 视频源
        private WaveInEvent waveIn;                // 音频输入设备
        private WaveOutEvent waveOut;              // 音频输出设备
        private BufferedWaveProvider bufferedWaveProvider; // 音频缓冲区
        private bool isWorking = true;
        private bool hasChanged = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mi_AutoResize.IsChecked = Properties.Settings.Default.AutoResize;

            Task.Run(() =>
            {
                try
                {
                    var target = WaitForDevice();
                    if (target == null) return;

                    InitializeVideo(target);
                    InitializeAudio();

                    StartVideoCapture();

                    Dispatcher.BeginInvoke(new Action(() =>
                        {
                            InitializeContextMenu();
                        }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }));

                }
            });
        }

        private void InitializeContextMenu()
        {
            try
            {
                mi_FrameSizeMenu.Items.Clear();
                for (int i = 0; i < videoSource.VideoCapabilities.Length; i++)
                {
                    var item = videoSource.VideoCapabilities[i];
                    var newrmi = new RadioMenuItem
                    {
                        Header = $"{item.FrameSize.Width}x{item.FrameSize.Height}@{item.AverageFrameRate}Hz",
                        GroupName = "FrameSize",
                        Value = i,
                        IsChecked = i == Properties.Settings.Default.FrameSizeIndex
                    };
                    newrmi.Click += Mi_FrameSizeM_Click;
                    mi_FrameSizeMenu.Items.Add(newrmi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeAudio()
        {
            waveIn = new WaveInEvent()
            {
                DeviceNumber = 0, // 选择第一个音频输入设备
                WaveFormat = new WaveFormat(44100, 2), // 44.1kHz, Stereo
                BufferMilliseconds = 20 // 设置缓冲区大小
            };
            waveIn.DataAvailable += (ss, ee) => bufferedWaveProvider.AddSamples(ee.Buffer, 0, ee.BytesRecorded);

            bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                DiscardOnBufferOverflow = true // 避免缓冲区无限增长
            };

            waveOut = new WaveOutEvent();
            waveOut.Init(bufferedWaveProvider);
        }

        private void InitializeVideo(FilterInfo target)
        {
            videoSource = new VideoCaptureDevice(target.MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame; // 订阅新帧事件

            videoSource.VideoResolution = videoSource.VideoCapabilities[Properties.Settings.Default.FrameSizeIndex]; // 选择分辨率
            videoSource.PlayingFinished += (ss, ee) =>
            {
                ScreensaverPreventer.AllowScreensaver();

                waveIn?.StopRecording();
                waveOut?.Stop();

                Task.Run(() =>
                {
                    if (WaitForDevice() == null) return;
                    StartVideoCapture();
                });
            };
        }
        private void StartVideoCapture()
        {
            videoSource.Start(); // 开始捕获
            waveOut.Play();
            waveIn.StartRecording();
            hasChanged = false;
            ScreensaverPreventer.PreventScreensaver();
        }

        private FilterInfo WaitForDevice()
        {
            FilterInfo target = null;
            while (target == null)
            {
                System.Threading.Thread.Sleep(500);
                if (!isWorking) return null;

                // 获取所有可用的视频设备
                target = new FilterInfoCollection(FilterCategory.VideoInputDevice)
                    .Cast<FilterInfo>()
                    .FirstOrDefault(t => t.Name == Properties.Settings.Default.VideoInputDevice);
            }
            return target;
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (Properties.Settings.Default.AutoResize && !hasChanged && this.WindowState != WindowState.Maximized)
                    {
                        this.Width = eventArgs.Frame.Width;
                        this.Height = eventArgs.Frame.Height + SystemParameters.WindowCaptionHeight + SystemParameters.WindowResizeBorderThickness.Bottom + SystemParameters.WindowResizeBorderThickness.Top;

                        this.Left = (SystemParameters.WorkArea.Width - this.ActualWidth) / 2;
                        this.Top = (SystemParameters.WorkArea.Height - this.ActualHeight) / 2;
                        hasChanged = true;
                    }
                    var bitmap = (System.Drawing.Bitmap)eventArgs.Frame.Clone();
                    CaptureImage.Source = ConvertBitmapToBitmapImage(bitmap);
                }
                catch (Exception) { }
            });
        }

        //private BitmapImage ConvertBitmapToBitmapImage(System.Drawing.Bitmap bitmap)
        //{
        //    var bitmapImage = new BitmapImage();
        //    using (var memory = new System.IO.MemoryStream())
        //    {
        //        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
        //        bitmapImage.BeginInit();
        //        bitmapImage.StreamSource = memory;
        //        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        //        bitmapImage.EndInit();
        //        bitmapImage.Freeze();
        //    }
        //    return bitmapImage;
        //}

        public static WriteableBitmap ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            var wb = new WriteableBitmap(
                bitmap.Width,
                bitmap.Height,
                96, 96, // DPI
                PixelFormats.Bgr24, // 匹配 GDI+ 的常见格式
                null);

            // 锁定并复制像素数据
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            try
            {
                wb.WritePixels(
                    new Int32Rect(0, 0, bitmap.Width, bitmap.Height),
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return wb;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isWorking = false;
            // 停止视频源
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.WindowStyle = this.WindowState == WindowState.Normal ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else if (e.Key == Key.Enter)
            {
                this.WindowStyle = this.WindowState == WindowState.Normal ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }

        }

        private void Mi_AutoResize_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoResize = mi_AutoResize.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void Mi_FrameSizeM_Click(object sender, RoutedEventArgs e)
        {
            videoSource.VideoResolution = videoSource.VideoCapabilities[Properties.Settings.Default.FrameSizeIndex]; // 选择分辨率
            videoSource?.SignalToStop();
        }
    }
}
