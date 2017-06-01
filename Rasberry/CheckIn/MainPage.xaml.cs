using FFmpegInterop;
using System;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls;
using Sensors.Dht;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using System.Diagnostics;
using Microsoft.ProjectOxford.Face;
using Windows.Media.FaceAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Graphics.Display;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Radios.nRF24L01P;
using Windows.Devices.Radios.nRF24L01P.Extensions;
using Windows.Devices.Radios.nRF24L01P.Interfaces;
using Windows.Devices.Spi;
using Windows.Devices.Radios.nRF24L01P.Roles;
using Common.Logging;
using Common.Logging.WinRT.Extras;
using System.Text;

namespace CheckIn
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            StartDHT11();
            InitializeRf24();
            StartDetectFace();
        }

        #region Stream

        private string StreamUrl = "rtsp://192.168.1.9:554/cam/realmonitor?channel=1&subtype=0";
        MediaStreamSource mss;

        private void DisplayErrorMessage(string text)
        {
            ToastNotifier ToastNotifier = ToastNotificationManager.CreateToastNotifier();
            Windows.Data.Xml.Dom.XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            Windows.Data.Xml.Dom.XmlNodeList toastNodeList = toastXml.GetElementsByTagName("text");
            toastNodeList.Item(0).AppendChild(toastXml.CreateTextNode(""));
            toastNodeList.Item(1).AppendChild(toastXml.CreateTextNode(text));
            Windows.Data.Xml.Dom.IXmlNode toastNode = toastXml.SelectSingleNode("/toast");

            ToastNotification toast = new ToastNotification(toastXml);
            toast.ExpirationTime = DateTime.Now.AddSeconds(4);
            ToastNotifier.Show(toast);
        }

        #endregion

        #region Face detection

        FaceDetector faceDetector;
        List<DetectedFace> detectedFaces;
        DispatcherTimer faceTimer;
        FaceDetection face;
        int lastfound;

        public async void StartDetectFace()
        {
            if (faceDetector == null)
            {
                faceDetector = await FaceDetector.CreateAsync();
            }

            if (faceTimer == null)
            {
                faceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                faceTimer.Tick += faceTimer_OnTick;
            }
            faceTimer.Start();

            face = new FaceDetection();
            await face.CreateRootGroup();
            //return;
            var folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("ML");// await StorageFolder.GetFolderFromPathAsync("C:\\Users\\Nhan\\Desktop\\ML");
            foreach (var f in await folder.GetFoldersAsync())
            {
                await Task.Run(async () =>
                {
                    await face.CreatePersonGroup(f.DisplayName, f);
                });
            }

            face.TrainGroup().ContinueWith(r => {
                Debug.WriteLine("Done");
            });
        }

        private async void faceTimer_OnTick(object sender, object e)
        {
            faceTimer.Stop();

            try
            {
                //var bitmap = await SaveVisualElementToFile(VLCStream);
                //var data = await faceDetector.DetectFacesAsync(bitmap);
                //Debug.WriteLine($"Found {data.Count} faces");

                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("a.jpg", CreationCollisionOption.ReplaceExisting);
                await SaveVisualElementToFile(VLCStream, file);
                IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                BitmapTransform transform = new BitmapTransform();
                const float sourceImageHeightLimit = 1280;

                if (decoder.PixelHeight > sourceImageHeightLimit)
                {
                    float scalingFactor = (float)sourceImageHeightLimit / (float)decoder.PixelHeight;
                    transform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * scalingFactor);
                    transform.ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * scalingFactor);
                }

                SoftwareBitmap sourceBitmap = await decoder.GetSoftwareBitmapAsync(
                    decoder.BitmapPixelFormat,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                const BitmapPixelFormat faceDetectionPixelFormat = BitmapPixelFormat.Gray8;

                SoftwareBitmap convertedBitmap;

                if (sourceBitmap.BitmapPixelFormat != faceDetectionPixelFormat)
                {
                    convertedBitmap = SoftwareBitmap.Convert(sourceBitmap, faceDetectionPixelFormat);
                }
                else
                {
                    convertedBitmap = sourceBitmap;
                }

                var data = await faceDetector.DetectFacesAsync(convertedBitmap);
                Debug.WriteLine($"Found {data.Count} faces");
                if(data.Count > 0)
                {
                    if (lastfound == data.Count) return;
                    lastfound = data.Count;

                    var results = await face.IdentifyAsync(file.Path);
                    if (results == null) return;
                    foreach (var identifyResult in results)
                    {
                        Debug.WriteLine("Result of face: {0}", identifyResult.FaceId);
                        if (identifyResult.Candidates.Length == 0)
                        {
                            Debug.WriteLine("No one identified");
                        }
                        else
                        {
                            // Get top 1 among all candidates returned
                            var candidateId = identifyResult.Candidates[0].PersonId;
                            var person = await face.GetPersonName(candidateId);

                            var text = $"Identified as {person.Name}";
                            Debug.WriteLine(text);
                            DisplayUtils.ShowToast(text);
                            SenderTest();
                        }
                    }
                }
            }
            catch(Exception ex) { Debug.WriteLine(ex.Message); }

            faceTimer.Start();
        }

        public static async Task SaveVisualElementToFile(FrameworkElement element, StorageFile file)
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(element);
            var pixels = await renderTargetBitmap.GetPixelsAsync();

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)renderTargetBitmap.PixelWidth,
                    (uint)renderTargetBitmap.PixelHeight,
                    DisplayInformation.GetForCurrentView().LogicalDpi,
                    DisplayInformation.GetForCurrentView().LogicalDpi,
                    pixels.ToArray());
                await encoder.FlushAsync();
            }
        }

        public async Task<SoftwareBitmap> SaveVisualElementToFile(FrameworkElement element)
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(element);
            var pixels = await renderTargetBitmap.GetPixelsAsync();

            //IRandomAccessStreamWithContentType stream = await mss.Thumbnail.OpenReadAsync();
            //BitmapImage bitmapImage = new BitmapImage();
            //bitmapImage.SetSource(stream);

            return SoftwareBitmap.CreateCopyFromBuffer(pixels, BitmapPixelFormat.Gray8,
                    Convert.ToInt32(VideoStream.ActualWidth), Convert.ToInt32(VideoStream.ActualHeight));
        }

        #endregion

        #region Temperature

        DispatcherTimer temperatureTimer;
        Dht11 dht11;

        private void StartDHT11()
        {
            try
            {
                GpioPin pin = GpioController.GetDefault().OpenPin(4, GpioSharingMode.Exclusive);
                dht11 = new Dht11(pin, GpioPinDriveMode.Input);

                temperatureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                temperatureTimer.Tick += OnTemperatureTimer_Tick;
                OnTemperatureTimer_Tick(temperatureTimer, null);
            }
            catch (Exception ex)
            {
                DisplayErrorMessage("No temperature sensor.");
            }
        }

        private async void OnTemperatureTimer_Tick(object sender, object e)
        {
            temperatureTimer.Stop();

            if(dht11 != null)
            {
                DhtReading reading = await dht11.GetReadingAsync().AsTask();
                Debug.WriteLine("Temperature: " + reading.Temperature);
                Debug.WriteLine("Humidity: " + reading.Humidity);
            }

            temperatureTimer.Start();
        }

        //public struct DhtReading
        //{
        //    bool TimedOut;
        //    bool IsValid;
        //    double Temperature;
        //    double Humidity;
        //    int RetryCount;
        //    public static implicit operator DhtReading(Sensors.Dht.DhtReading v) { throw new NotImplementedException(); }
        //};

        #endregion

        #region RF24

        private Radio _radio;
        private readonly byte[] _sendAddress = new byte[] { 0x54, 0x4d, 0x52, 0x68, 0x7C };
        private readonly byte[] _receiveAddress = new byte[] { 0xAB, 0xCD, 0xAB, 0xCD, 0x71 };
        private readonly object _syncRoot = new object();
        byte[] dataOpen = { 0b0000_0001, 0b1111_1111 };
        byte[] dataClose = { 0b0000_0001, 0b0000_0000 };

        public void InitializeRf24()
        {
            GpioController gpioController = GpioController.GetDefault();

            GpioPin powerPin = gpioController.InitGpioPin(4, GpioPinDriveMode.Output, GpioSharingMode.Exclusive);
            GpioPin cePin = gpioController.InitGpioPin(9, GpioPinDriveMode.Output, GpioSharingMode.Exclusive);
            GpioPin irqPin = gpioController.InitGpioPin(25, GpioPinDriveMode.InputPullUp, GpioSharingMode.Exclusive);
            powerPin.Write(GpioPinValue.Low);
            cePin.Write(GpioPinValue.Low);
            irqPin.Write(GpioPinValue.High);

            DeviceInformationCollection devicesInfo = DeviceInformation.FindAllAsync(SpiDevice.GetDeviceSelector("SPI0")).GetAwaiter().GetResult();
            SpiDevice spiDevice = SpiDevice.FromIdAsync(devicesInfo[0].Id, new SpiConnectionSettings(0)).GetAwaiter().GetResult();
            ICommandProcessor commandProcessor = new CommandProcessor(spiDevice);

            ILoggerFactoryAdapter loggerFactoryAdapter = new DebugOutLoggerFactoryAdapter(LogLevel.All, true, true, true, "MM/dd/yyyy hh:mm:ss fffff");

            var _radio = new Radio(commandProcessor, loggerFactoryAdapter, powerPin, cePin, irqPin);

            //SenderTest();
            //ReceiverTest();
            SenderReceiverTest();
        }
        public void ReceiverTest()
        {
            ReceiverRole receiver = new ReceiverRole();
            receiver.AttachRadio(_radio);
            receiver.ReceiveAddress = _receiveAddress;
            receiver.DataArrived += DataArrived; ;
            receiver.Start();
            while (true) { }
        }

        public void SenderTest()
        {
            SenderRole sender = new SenderRole();
            sender.AttachRadio(_radio);
            sender.SendAddress = _sendAddress;
            sender.Start();
            int count = 0;
            while (true)
            {
                string content = "Payload, Count=" + (count++);
                byte[] buffer = dataOpen;
                lock (_syncRoot)
                {
                    //Encoding.UTF8.GetBytes(content).ReverseBytes()
                    Debug.WriteLine(sender.Send(buffer.ReverseBytes())
                        ? "Send complete"
                        : "Send failed " + (sender.MaxRetries ? "MaxRetries" : "Timeout"));
                }
                Task.Delay(1000).Wait();
            }
        }

        public void SenderReceiverTest()
        {
            SenderReceiverRole senderReceiver = new SenderReceiverRole();
            senderReceiver.AttachRadio(_radio);
            senderReceiver.DataArrived += DataArrived; ;
            senderReceiver.SendAddress = _sendAddress;
            senderReceiver.ReceiveAddress = _receiveAddress;
            senderReceiver.Start();
            int count = 0;
            while (true)
            {
                string content = "Payload, Count=" + (count++);
                lock (_syncRoot)
                {
                    Debug.WriteLine(senderReceiver.Send(Encoding.UTF8.GetBytes(content).ReverseBytes(), 5000)
                        ? "Data sent success."
                        : "Failed to send data. " + (senderReceiver.MaxRetries ? "MaxRetries" : "Timeout"));
                }
                Task.Delay(1000).Wait();
            }
        }

        private void DataArrived(object sender, byte[] data)
        {
            string content = Encoding.UTF8.GetString(data, 0, data.Length - 1);
            Debug.WriteLine("Data Received, Data = " + content);
        }

        public void Dispose()
        {
            _radio?.Dispose();
            _radio = null;
        }


        #endregion

    }
}
