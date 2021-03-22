namespace ScreenshotTranslator
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
    using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Diagnostics;
    using System.Drawing;
    using Microsoft.Azure.Devices.Shared; 

    class Program
    {
        static int counter;
        static FileSystemWatcher watcher;
        private const String logoImagePath = "/app/logo.png";
        private const String screenShotPath ="/storage/screenshots/";
        private const String translatedScreenShotPath="/storage/screenshots/translated/";
        static string FontFamily = "DejaVuSansMono-Bold";
        static int FontSize { get; set; } = 25;
        static string Language { get; set; } = "en";
        private static string TranslatorTextApiKey = null;
        private const string TranslatorTextEndpoint = "https://api.cognitive.microsofttranslator.com";
        private const string RecognizeTextEndpoint = "http://cognitive-services-recognize-text:5000";
        private static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            try
            {
                TranslatorTextApiKey=args[0].Split("=")[1];
            }
            catch
            {
                Console.WriteLine("ERROR - Expected Argument for TranslatorTextApiKey");
            }

            if(TranslatorTextApiKey.Length != 32)
                Console.WriteLine("WARNING - TranslatorTextApiKey is not set or formatted appropriately");

            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Read the TemperatureThreshold value from the module twin's desired properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            InitFileWatcher();

            DisplayImage(logoImagePath);

            Console.WriteLine("Successfully initialized ScreenshotWatcher module.");     
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                if (desiredProperties["FontSize"]!=null)
                    FontSize = desiredProperties["FontSize"];

                if (desiredProperties["Language"]!=null)
                    Language = desiredProperties["Language"];

                if (desiredProperties["FontFamily"]!=null)
                    FontFamily = desiredProperties["FontFamily"];

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }        

        private static void InitFileWatcher(){
            
            // Create a new FileSystemWatcher and set its properties.
            watcher = new FileSystemWatcher();
            watcher.Path = screenShotPath;

            // Watch for changes in CreationTimes
            watcher.NotifyFilter = NotifyFilters.FileName;

            // Only watch text files.
            watcher.Filter = "*.png";

            // Add event handlers.
            watcher.Created += OnChanged;

            // Begin watching.
            watcher.EnableRaisingEvents = true;
        }

        // Define event handlers.
        private static async void OnChanged(object source, FileSystemEventArgs e)
        {
        // Specify what is done when a file is changed, created, or deleted.
        Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");

            var image = e.FullPath;

            if (!File.Exists(image))
            {
                Console.WriteLine($"Unable to open or read '{image}'");
                Environment.Exit(-1);
            }

            try
            {
                //Wait for file to finish writing
                await Task.Delay(1000);

                Console.WriteLine($"Extracting text from '{image}'");
                Stream imageStream = File.OpenRead(image);
                var resultAsJson = await ExtractText(imageStream);
                
                //Wait for file to finish closing
                await Task.Delay(1000);

                imageStream = File.OpenRead(image);
                
                var translatedImage = await TranslateImage(new Bitmap(imageStream), resultAsJson, image.Remove(0,screenShotPath.Length));
                DisplayImage(translatedImage);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception encountered processing: '{image}'");
                Console.WriteLine(ex.Message);
            }
        
        }
        private static async Task<JObject> ExtractText(Stream imageStream)
        {

            String responseString = string.Empty;
            using (var imageContent = new StreamContent(imageStream))
            {
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var requestAddress = RecognizeTextEndpoint + "/vision/v3.2-preview.2/read/syncAnalyze";

                using (var response = await client.PostAsync(requestAddress, imageContent))
                {
                    var resultAsString = await response.Content.ReadAsStringAsync();
                    var resultAsJson = JsonConvert.DeserializeObject<JObject>(resultAsString);

                    if (resultAsJson["lines"] == null)
                    {
                        responseString = resultAsString;
                    }
                    else
                    {
                        foreach (var line in resultAsJson["lines"])
                        {
                            responseString += line["text"] + "\n";
                        }
                    }

                    Console.WriteLine(responseString);
                    return resultAsJson;
                }
            }
        }

        private static async Task<String> TranslateImage(Image image, JObject resultAsJson, string fileName)
        {
            string translatedImage = translatedScreenShotPath + fileName;

            Graphics graph = Graphics.FromImage(image);
            
            //Blackout original Text
            foreach (var line in resultAsJson["analyzeResult"]["readResults"][0]["lines"])
            {          

                // Create points that define polygon.
                PointF point1 = new PointF((int)line["boundingBox"][0],(int)line["boundingBox"][1]);
                PointF point2 = new PointF((int)line["boundingBox"][2],(int)line["boundingBox"][3]);
                PointF point3 = new PointF((int)line["boundingBox"][4],(int)line["boundingBox"][5]);
                PointF point4 = new PointF((int)line["boundingBox"][6],(int)line["boundingBox"][7]);
                PointF[] curvePoints =
                        {
                            point1,
                            point2,
                            point3,
                            point4,
                        };
                        
                // Draw polygon curve to screen.
                graph.FillPolygon(Brushes.Black, curvePoints);
            }

            //Translate and print Text
            foreach (var line in resultAsJson["analyzeResult"]["readResults"][0]["lines"])
            {          
                string translatedText = string.Empty;

                translatedText = await TranslateText(line["text"] + "\n");

                int x = (int)line["boundingBox"][0];
                int y = (int)line["boundingBox"][1];

                graph.DrawString(translatedText, 
                    new Font(new FontFamily(FontFamily), FontSize), 
                    Brushes.White, new PointF(x, y));

            }

            System.IO.Directory.CreateDirectory(translatedScreenShotPath);
            image.Save(translatedImage, System.Drawing.Imaging.ImageFormat.Png);
            return translatedImage;
        }

        public static async Task<String> TranslateText(string text)
        {
            var requestAddress = TranslatorTextEndpoint + "/translate?api-version=3.0&to=" + Language;
            System.Object[] body = new System.Object[] { new { Text = text }};
            var requestBody = JsonConvert.SerializeObject(body);

            using (var request = new HttpRequestMessage())
            {
                // Set the method to POST
                request.Method = HttpMethod.Post;

                // Construct the full URI
                request.RequestUri = new Uri(requestAddress);

                // Add the serialized JSON object to your request
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Add the authorization header
                request.Headers.Add("Ocp-Apim-Subscription-Key", TranslatorTextApiKey);

                // Send request, get response
                var response = client.SendAsync(request).Result;
                var resultAsString = await response.Content.ReadAsStringAsync();
                var resultAsJson = JsonConvert.DeserializeObject<JArray>(resultAsString);

                // Print the response
                Console.WriteLine("Text translated to " + Language);
                Console.WriteLine(resultAsString);
                string translatedText = string.Empty;
                try
                {
                    translatedText = resultAsJson[0]["translations"][0]["text"] + "\n";
                }
                catch
                {
                    translatedText = "N/A";
                }

                return translatedText;
            }
        }

        /// <summary>
        /// Displays image to fb0 using fbi
        /// </summary>
        private static void DisplayImage(string image)
        {        
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "fbi",
                    Arguments = $"-d /dev/fb0 -T 1 -noverbose -a \"{image}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            Console.WriteLine($"Displaying image: '{image}'");
            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var pipeMessage = new Message(messageBytes);
                foreach (var prop in message.Properties)
                {
                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                }
                await moduleClient.SendEventAsync("output1", pipeMessage);
                Console.WriteLine("Received message sent");
            }
            return MessageResponse.Completed;
        }
    }
}
