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
            Loaded += MainWindow_Loaded; // 订阅窗口加载事件
            Closing += MainWindow_Closing; // 订阅窗口关闭事件
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mi_AutoResize.IsChecked = Properties.Settings.Default.AutoResize;

            Task.Run(() =>
            {
                while (isWorking)
                {
                    if (videoSource == null)
                    {
                        var target = WaitForDevice();
                        if (target == null) return;

                        Dispatcher.Invoke(() =>
                        {
                            InitializeVideoSource(target);
                            InitializeAudio();
                        });
                    }

                    if (videoSource?.IsRunning == false)
                    {
                        var target = WaitForDevice();
                        if (target == null) return;

                        StartVideoCapture();
                    }

                    System.Threading.Thread.Sleep(500);
                }
            });
        }

        private void InitializeVideoSource(FilterInfo target)
        {
            videoSource = new VideoCaptureDevice(target.MonikerString);
            PopulateFrameSizeMenu();
            var rmi = mi_FrameSizeMenu.Items.OfType<RadioMenuItem>().FirstOrDefault(i => i.Value == Properties.Settings.Default.FrameSizeIndex);
            rmi?.SetCurrentValue(MenuItem.IsCheckedProperty, true);
        }

        private void PopulateFrameSizeMenu()
        {
            mi_FrameSizeMenu.Items.Clear();
            for (int i = 0; i < videoSource.VideoCapabilities.Length; i++)
            {
                var item = videoSource.VideoCapabilities[i];
                var newrmi = new RadioMenuItem
                {
                    Header = $"{item.FrameSize.Width}x{item.FrameSize.Height}",
                    GroupName = "FrameSize",
                    Value = i,
                    IsChecked = i == Properties.Settings.Default.FrameSizeIndex
                };
                newrmi.Click += Mi_FrameSizeM_Click;
                mi_FrameSizeMenu.Items.Add(newrmi);
            }
        }

        private void InitializeAudio()
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = 0, // 选择第一个音频输入设备
                WaveFormat = new WaveFormat(44100, 2) // 44.1kHz, Stereo
            };
            waveIn.DataAvailable += (ss, ee) => bufferedWaveProvider.AddSamples(ee.Buffer, 0, ee.BytesRecorded);

            bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
            waveOut = new WaveOutEvent();
            waveOut.Init(bufferedWaveProvider);
        }

        private void StartVideoCapture()
        {
            videoSource.VideoResolution = videoSource.VideoCapabilities[Properties.Settings.Default.FrameSizeIndex]; // 选择分辨率
            videoSource.NewFrame += VideoSource_NewFrame; // 订阅新帧事件
            videoSource.PlayingFinished += (ss, ee) =>
            {
                waveIn?.StopRecording();
                waveOut?.Stop();
            };

            videoSource.Start(); // 开始捕获
            waveOut.Play();
            waveIn.StartRecording();
            hasChanged = false;
        }

        private FilterInfo WaitForDevice()
        {
            FilterInfo target = null;
            while (target == null)
            {
                System.Threading.Thread.Sleep(100);
                if (!isWorking) return null;

                // 获取所有可用的视频设备
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                target = videoDevices.Cast<FilterInfo>().FirstOrDefault(t => t.Name == "HDMI Capture");
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

        private BitmapImage ConvertBitmapToBitmapImage(System.Drawing.Bitmap bitmap)
        {
            var bitmapImage = new BitmapImage();
            using (var memory = new System.IO.MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
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
            }else if (e.Key == Key.Enter)
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
            videoSource?.SignalToStop();
        }
    }
}
