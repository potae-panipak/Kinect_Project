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
    public partial class MainWindow : Window
    {

        int skeletonDetectedCount = 0;
        int ObjectAlertCount = 0;
        int DarknessAlertCount = 0;
        BitmapSource inputColorBitmap;
        public MainWindow()
        {
            InitializeComponent();
        }

        async private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            await CallLineApiService().ConfigureAwait(true);
            Device device;
            try
            {
                using (device = Device.Open(0))
                {
                    device.StartCameras(new DeviceConfiguration
                    {
                        ColorFormat = ImageFormat.ColorBGRA32,
                        ColorResolution = ColorResolution.R720p,
                        DepthMode = DepthMode.NFOV_Unbinned,
                        SynchronizedImagesOnly = true,
                        CameraFPS = FPS.FPS30,
                    });
                    int colorWidth = device.GetCalibration().ColorCameraCalibration.ResolutionWidth;
                    int colorHeight = device.GetCalibration().ColorCameraCalibration.ResolutionHeight;
                    var callibration = device.GetCalibration(DepthMode.NFOV_Unbinned, ColorResolution.R720p);
                    var trackerConfig = new TrackerConfiguration();
                    trackerConfig.ProcessingMode = TrackerProcessingMode.Gpu;
                    trackerConfig.SensorOrientation = SensorOrientation.Default;
                    using (var tracker = Tracker.Create(callibration, trackerConfig))
                    {
                        using (Transformation transform = device.GetCalibration().CreateTransformation())
                        {

                            while (true)
                            {
                                using (Capture capture = await Task.Run(() => { return device.GetCapture(); }).ConfigureAwait(true))
                                {
                                    Task<BitmapSource> createInputColorBitmapTask = Task.Run(() =>
                                    {
                                        Image color = capture.Color;
                                        BitmapSource source = BitmapSource.Create(color.WidthPixels, color.HeightPixels, 96, 96, PixelFormats.Bgra32, null, color.Memory.ToArray(), color.StrideBytes);
                                        source.Freeze();
                                        return source;
                                    });
                                    this.inputColorBitmap = await createInputColorBitmapTask.ConfigureAwait(true);
                                    this.InputColorImageViewPane.Source = inputColorBitmap;
                                    tracker.EnqueueCapture(capture);
                                    using (Microsoft.Azure.Kinect.BodyTracking.Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
                                    {
                                        if (frame != null)
                                        {
                                            Console.WriteLine("Number Body: " + frame.NumberOfBodies);
                                            if (frame.NumberOfBodies > 0)
                                            {
                                                await SaveFile(this.inputColorBitmap);
                                                if(await callDetectDarknessServices().ConfigureAwait(true))
                                                {
                                                    speech("The room is too dark please turn on the light.");
                                                    this.DarknessAlertCount = this.DarknessAlertCount + 1; ;
                                                }
                                                else if (await callDetectionObjectServices().ConfigureAwait(true))
                                                {
                                                    speech("Please Beware of Object On the Floor. Please Beware of Object On the Floor.");
                                                    this.ObjectAlertCount = this.ObjectAlertCount + 1; ;
                                                }
                                            }
                                        }
                                    }
                                    
                                }
                                switch((DateTime.Now.ToString("HH:mm", System.Globalization.DateTimeFormatInfo.InvariantInfo))){
                                    case "00:00":
                                    case "00:01":
                                    case "00:02":this.ObjectAlertCount = 0; this.DarknessAlertCount = 0; break;
                                    default: break;
                                }
                                await CallLineApiService().ConfigureAwait(true);
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
            this.skeletonDetectedCount = this.skeletonDetectedCount + 1;
            filename = filename + this.skeletonDetectedCount.ToString();
            filename = filename + ".png";
            using (var fileStream = new FileStream(filename, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(picture));
                encoder.Save(fileStream);
                fileStream.Close();
            }
        }
        private async Task<Boolean> callDetectionObjectServices()
        {
            string ENDPOINT = "https://southcentralus.api.cognitive.microsoft.com";
            string predictionKey = "2d24f7776af445939b7612cb8f1c2e64";
            CustomVisionPredictionClient predictionApi = AuthenticatePrediction(ENDPOINT, predictionKey);
            return DetectionTest(predictionApi);
        }
        private async Task<Boolean> callDetectDarknessServices()
        {
            string ENDPOINT = "https://southcentralus.api.cognitive.microsoft.com";
            string predictionKey = "2d24f7776af445939b7612cb8f1c2e64";
            CustomVisionPredictionClient predictionApi = AuthenticatePrediction(ENDPOINT, predictionKey);
            return ClassificationTest(predictionApi);
        }
        private CustomVisionPredictionClient AuthenticatePrediction(string endpoint, string predictionKey)
        {
            CustomVisionPredictionClient predictionApi = new CustomVisionPredictionClient(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.ApiKeyServiceClientCredentials(predictionKey))
            {
                Endpoint = endpoint
            };
            return predictionApi;
        }
        private Boolean DetectionTest(CustomVisionPredictionClient predictionApi)
        {
            Console.WriteLine("Making a prediction:");
            String filename = "C:\\Users\\User\\Desktop\\TestRec\\Pic\\KinectCapture";
            filename = filename + this.skeletonDetectedCount.ToString();
            filename = filename + ".png";
            using (var stream = File.OpenRead(filename))
            {
                System.Guid projectID = new Guid("22a12a4d-2c2d-49f7-a6c3-4ff7bee6b15b"); //เอาจากlinkของ Project ใน customvision.ai/projects/<ProjectID>#/manage
                var result = predictionApi.DetectImage(projectID, "ObjDetection", stream);
                foreach (var c in result.Predictions)
                {
                    Console.WriteLine($"\t{c.TagName}: {c.Probability:P1} [ Left:{c.BoundingBox.Left}, Top:{c.BoundingBox.Top}, Width:{c.BoundingBox.Width}, Height:{c.BoundingBox.Height} ]");
                    if (c.Probability > 0.5)
                    {
                        if (c.BoundingBox.Left < 0.35 || c.BoundingBox.Top < 0.1 || c.BoundingBox.Left > 0.61 || c.BoundingBox.Top > 0.4) return true;
                    }
                }
                return false;
            }
        }
        private Boolean ClassificationTest(CustomVisionPredictionClient predictionApi)
        {
            Console.WriteLine("Making a prediction:");
            String filename = "C:\\Users\\User\\Desktop\\TestRec\\Pic\\KinectCapture";
            filename = filename + this.skeletonDetectedCount.ToString();
            filename = filename + ".png";
            using (var stream = File.OpenRead(filename))
            {
                System.Guid projectID = new Guid("8267e789-cff6-4c76-a901-782c6dfc3f04"); //เอาจากlinkของ Project ใน customvision.ai/projects/<ProjectID>#/manage
                Console.WriteLine("Making a prediction:");
                var result = predictionApi.ClassifyImage(projectID, "lightdark", stream);

                // Loop over each prediction and write out the results
                foreach (var c in result.Predictions)
                {
                    Console.WriteLine($"\t{c.TagName}: {c.Probability:P1}");
                    if (c.Probability > 0.6 && c.TagName == "dark") return true;
                }
                return false;
            }
        }
        private void speech(string text)
        {
            SpeechSynthesizer _SS = new SpeechSynthesizer();
            _SS.SelectVoiceByHints(VoiceGender.Female);
            _SS.Speak(text);
        }
        private async Task CallLineApiService()
        {
            var client = new HttpClient();
            string url = "https://kinect-chai4.herokuapp.com/AzureDetect";
            HttpResponseMessage response;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            dataToSend data = new dataToSend();
            data.AlertObject = this.ObjectAlertCount;
            data.AlertDarkness = this.DarknessAlertCount;
            String json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json);
            response = await client.PostAsync(url, content);
        }
    }

    class dataToSend
    {
        public int AlertObject;
        public int AlertDarkness;
    }
}

