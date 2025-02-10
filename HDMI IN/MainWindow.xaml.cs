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
        bool hasChanged = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded; // 订阅窗口加载事件
            Closing += MainWindow_Closing; // 订阅窗口关闭事件
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            mi_AutoResize.IsChecked = Properties.Settings.Default.AutoResize;
            var rmi = mi_FrameSizeMenu.Items.OfType<RadioMenuItem>().FirstOrDefault(i => i.Value == Properties.Settings.Default.FrameSizeIndex);
            if (null != rmi) rmi.IsChecked = true;

            Task.Run(() =>
            {
                try
                {
                    while (isWorking)
                    {
                        if (videoSource == null || videoSource.IsRunning == false)
                        {

                            FilterInfo target = null;
                            while (target == null)
                            {
                                System.Threading.Thread.Sleep(100);

                                if (!isWorking) return;

                                // 获取所有可用的视频设备
                                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                                target = videoDevices.Cast<FilterInfo>().ToList().FirstOrDefault(t => t.Name == "HDMI Capture");
                            };

                            // 选择第一个设备
                            videoSource = new VideoCaptureDevice(target.MonikerString);
                            videoSource.VideoResolution = videoSource.VideoCapabilities[Properties.Settings.Default.FrameSizeIndex]; // 选择最大分辨率
                            videoSource.NewFrame += VideoSource_NewFrame; // 订阅新帧事件
                            videoSource.PlayingFinished += (ss, ee) =>
                            {
                                if (waveIn != null)
                                {
                                    waveIn.StopRecording();
                                    waveIn.Dispose();
                                }
                                if (waveOut != null)
                                {
                                    waveOut.Stop();
                                    waveOut.Dispose();
                                }
                            };
                            videoSource.Start(); // 开始捕获

                            var waveInDevices = WaveInEvent.DeviceCount;

                            waveIn = new WaveInEvent();
                            waveIn.DeviceNumber = 0; // 选择第一个音频输入设备
                            waveIn.DataAvailable += WaveIn_DataAvailable;
                            waveIn.WaveFormat = new WaveFormat(44100, 2); // 44.1kHz, Mono

                            bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
                            waveOut = new WaveOutEvent();
                            waveOut.Init(bufferedWaveProvider);
                            waveOut.Play();

                            waveIn.StartRecording();

                            hasChanged = false;
                        }
                        System.Threading.Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                };
            });
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
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

                        var screenWidth = SystemParameters.WorkArea.Width;
                        var screenHeight = SystemParameters.WorkArea.Height;

                        this.Left = (screenWidth - this.ActualWidth) / 2;
                        this.Top = (screenHeight - this.ActualHeight) / 2;

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

                return bitmapImage;
            }
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
                if (this.WindowState == WindowState.Normal)
                {
                    this.WindowStyle = WindowStyle.None;
                }
                else
                {
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                }
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
        }

        private void mi_AutoResize_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoResize = mi_AutoResize.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void mi_FrameSizeM_Click(object sender, RoutedEventArgs e)
        {
            videoSource.SignalToStop();
        }
    }
}
