using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Azure.Kinect.Sensor;
using Image = Microsoft.Azure.Kinect.Sensor.Image;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Speech.Synthesis;
using Microsoft.Azure.Kinect.BodyTracking;

namespace Kinect_Final_Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        int count = 0;
        BitmapSource inputColorBitmap;
        public MainWindow()
        {
            InitializeComponent();
        }

        async private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //speech();
            Device device;
            try
            {
                using (device = Device.Open(0))
                {
                    device.StartCameras(new DeviceConfiguration
                    {
                        ColorFormat = ImageFormat.ColorBGRA32,
                        ColorResolution = ColorResolution.R1080p,
                        DepthMode = DepthMode.NFOV_Unbinned,
                        SynchronizedImagesOnly = true,
                        CameraFPS = FPS.FPS30,
                    });
                    int colorWidth = device.GetCalibration().ColorCameraCalibration.ResolutionWidth;
                    int colorHeight = device.GetCalibration().ColorCameraCalibration.ResolutionHeight;
                    var callibration = device.GetCalibration(DepthMode.NFOV_Unbinned, ColorResolution.R1080p);
                    var trackerConfig = new TrackerConfiguration();
                    trackerConfig.ProcessingMode = TrackerProcessingMode.Gpu;
                    trackerConfig.SensorOrientation = SensorOrientation.Default;
                    using (var tracker = Tracker.Create(callibration, trackerConfig))
                    {
                        using (Transformation transform = device.GetCalibration().CreateTransformation())
                        {

                            while (this.IsActive)
                            {
                                using (Capture capture = await Task.Run(() => { return device.GetCapture(); }).ConfigureAwait(true))
                                {
                                    tracker.EnqueueCapture(capture);
                                    using (Microsoft.Azure.Kinect.BodyTracking.Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
                                    {
                                        if (frame != null)
                                        {
                                            Console.WriteLine("Number Body: " + frame.NumberOfBodies);
                                            if (frame.NumberOfBodies > 0)
                                            {
                                                await SaveFile(this.inputColorBitmap).ConfigureAwait(true);
                                                await callServices().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    Task<BitmapSource> createInputColorBitmapTask = Task.Run(() =>
                                    {
                                        Image color = capture.Color;
                                        BitmapSource source = BitmapSource.Create(color.WidthPixels, color.HeightPixels, 96, 96, PixelFormats.Bgra32, null, color.Memory.ToArray(), color.StrideBytes);
                                        source.Freeze();
                                        return source;
                                    });
                                    this.inputColorBitmap = await createInputColorBitmapTask.ConfigureAwait(true);
                                    this.InputColorImageViewPane.Source = inputColorBitmap;
                                }
                            }

                        }
                    }
                }

            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private async Task SaveFile(BitmapSource picture)
        {
            String filename = "C:\\Users\\User\\Desktop\\TestRec\\Pic\\KinectCapture";
            this.count = this.count + 1;
            filename = filename + this.count.ToString();
            filename = filename + ".png";
            using (var fileStream = new FileStream(filename, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(picture));
                encoder.Save(fileStream);
                fileStream.Close();
            }
        }
        private async Task callServices()
        {
            string ENDPOINT = "https://southcentralus.api.cognitive.microsoft.com";
            string predictionKey = "5a187b28a20a43deb16f212f178c33ee";
            CustomVisionPredictionClient predictionApi = AuthenticatePrediction(ENDPOINT, predictionKey);
            TestIteration(predictionApi);
        }
        private CustomVisionPredictionClient AuthenticatePrediction(string endpoint, string predictionKey)
        {
            // Create a prediction endpoint, passing in the obtained prediction key
            CustomVisionPredictionClient predictionApi = new CustomVisionPredictionClient(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.ApiKeyServiceClientCredentials(predictionKey))
            {
                Endpoint = endpoint
            };
            return predictionApi;
        }
        private void TestIteration(CustomVisionPredictionClient predictionApi)
        {
            // Make a prediction against the new project
            Console.WriteLine("Making a prediction:");
            //var imageFile = System.IO.Path.Combine("Images", "test", "test_image.jpg");
            //var imageFile = System.IO.Path.Combine("C:", "Users","User","Desktop","TestRec","Pic","test.jpg");
            String filename = "C:\\Users\\User\\Desktop\\TestRec\\Pic\\KinectCapture";
            filename = filename + this.count.ToString();
            filename = filename + ".png";
            using (var stream = File.OpenRead(filename))
            {
                System.Guid projectID = new Guid("cef93cf3-4455-4606-b5fc-3872bb4575af"); //เอาจากlinkของ Project ใน customvision.ai/projects/<ProjectID>#/manage
                var result = predictionApi.DetectImage(projectID, "test", stream);
                // Loop over each prediction and write out the results
                Console.WriteLine(result.Predictions.Count);
                foreach (var c in result.Predictions)
                {
                    Console.WriteLine($"\t{c.TagName}: {c.Probability:P1} [ Left:{c.BoundingBox.Left}, Top:{c.BoundingBox.Top}, Width:{c.BoundingBox.Width}, Height:{c.BoundingBox.Height} ]");
                }
            }
        }
        private void speech()
        {
            SpeechSynthesizer _SS = new SpeechSynthesizer();
            String text = "hello";
            _SS.SelectVoiceByHints(VoiceGender.Female);
            _SS.Speak(text);
        }
    }
}
