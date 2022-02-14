using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text;
// Import namespaces
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace image_analysis
{
    class Program
    {

        private static ComputerVisionClient cvClient;
        static async Task Main(string[] args)
        {
            try
            {
                // Get config settings from AppSettings
                IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
                IConfigurationRoot configuration = builder.Build();
                string cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
                string cogSvcKey = configuration["CognitiveServiceKey"];

                // Get image
                string imageFile = "image/building.jpg";

                string fileName = Path.Combine(Environment.CurrentDirectory, "analyze.txt");
                if (args.Length > 0)
                {
                    imageFile = args[0];
                }


                // Authenticate Computer Vision client
                ApiKeyServiceClientCredentials credentials = new ApiKeyServiceClientCredentials(cogSvcKey);
                cvClient = new ComputerVisionClient(credentials)
                {
                    Endpoint = cogSvcEndpoint
                };

                // Analyze image
                await AnalyzeImage(imageFile, fileName);

                // Get thumbnail
                await GetThumbnail(imageFile);

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task AnalyzeImage(string imageFile, string fileName)
        {
            Console.WriteLine($"Analyzing {imageFile}");

            // Specify features to be retrieved
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                VisualFeatureTypes.Description,
                VisualFeatureTypes.Tags,
                VisualFeatureTypes.Categories,
                VisualFeatureTypes.Brands,
                VisualFeatureTypes.Objects,
                VisualFeatureTypes.Adult
            };

            // Get image analysis
            using(StreamWriter sw = new StreamWriter(fileName, false)){
                
                using (var imageData = File.OpenRead(imageFile))
                {    
                    var analysis = await cvClient.AnalyzeImageInStreamAsync(imageData, features);

                    // get image captions
                    foreach (var caption in analysis.Description.Captions)
                    {
                        sw.WriteLine($"Description: {caption.Text} (confidence: {caption.Confidence.ToString("P")})");
                    }

                    // Get image tags
                    if (analysis.Tags.Count > 0)
                    {
                        sw.WriteLine("Tags:");
                        foreach (var tag in analysis.Tags)
                        {
                            sw.WriteLine($" -{tag.Name} (confidence: {tag.Confidence.ToString("P")})");
                        }
                    }

                    // Get image categories
                    List<LandmarksModel> landmarks = new List<LandmarksModel> {};
                    List<CelebritiesModel> celebrities = new List<CelebritiesModel> {};
                    sw.WriteLine("Categories:");
                    foreach (var category in analysis.Categories)
                    {
                        // Print the category
                        sw.WriteLine($" -{category.Name} (confidence: {category.Score.ToString("P")})");
                        AddImageToFolder(imageFile, category.Name);
                        // Get landmarks in this category
                        if (category.Detail?.Landmarks != null)
                        {
                            foreach (LandmarksModel landmark in category.Detail.Landmarks)
                            {
                                if (!landmarks.Any(item => item.Name == landmark.Name))
                                {
                                    landmarks.Add(landmark);
                                }
                            }
                        }

                        // Get celebrities in this category
                        if (category.Detail?.Celebrities != null)
                        {
                            foreach (CelebritiesModel celebrity in category.Detail.Celebrities)
                            {
                                if (!celebrities.Any(item => item.Name == celebrity.Name))
                                {
                                    celebrities.Add(celebrity);
                                }
                            }
                        }
                    }
                    // If there were landmarks, list them
                    if (landmarks.Count > 0)
                    {
                        sw.WriteLine("Landmarks:");
                        foreach(LandmarksModel landmark in landmarks)
                        {
                            sw.WriteLine($" -{landmark.Name} (confidence: {landmark.Confidence.ToString("P")})");
                        }
                    }

                    // If there were celebrities, list them
                    if (celebrities.Count > 0)
                    {
                        sw.WriteLine("Celebrities:");
                        foreach(CelebritiesModel celebrity in celebrities)
                        {
                            sw.WriteLine($" -{celebrity.Name} (confidence: {celebrity.Confidence.ToString("P")})");
                        }
                    }                   
                    // Get brands in the image
                    if (analysis.Brands.Count > 0)
                    {
                        sw.WriteLine("Brands:");
                        foreach (var brand in analysis.Brands)
                        {
                            sw.WriteLine($" -{brand.Name} (confidence: {brand.Confidence.ToString("P")})");
                        }
                    }

                    // Get objects in the image
                    if (analysis.Objects.Count > 0)
                    {
                        sw.WriteLine("Objects in image:");

                        // Prepare image for drawing
                        Image image = Image.FromFile(imageFile);
                        Graphics graphics = Graphics.FromImage(image);
                        Pen pen = new Pen(Color.Cyan, 3);
                        Font font = new Font("Arial", 16);
                        SolidBrush brush = new SolidBrush(Color.Black);

                        foreach (var detectedObject in analysis.Objects)
                        {
                            // Print object name
                            sw.WriteLine($" -{detectedObject.ObjectProperty} (confidence: {detectedObject.Confidence.ToString("P")})");

                            // Draw object bounding box
                            var r = detectedObject.Rectangle;
                            Rectangle rect = new Rectangle(r.X, r.Y, r.W, r.H);
                            graphics.DrawRectangle(pen, rect);
                            graphics.DrawString(detectedObject.ObjectProperty,font,brush,r.X, r.Y);

                        }
                        // Save annotated image
                        String output_file = "objects.jpg";
                        image.Save(output_file);
                        sw.WriteLine("  Results saved in " + output_file);   
                    }
                    // Get moderation ratings
                    string ratings = $"Ratings:\n -Adult: {analysis.Adult.IsAdultContent}\n -Racy: {analysis.Adult.IsRacyContent}\n -Gore: {analysis.Adult.IsGoryContent}";
                    sw.WriteLine(ratings);

                    
                } 
            }               
        }
        
        static async Task GetThumbnail(string imageFile)
        {
            Console.WriteLine("Generating thumbnail");

            // Generate a thumbnail
            using (var imageData = File.OpenRead(imageFile))
            {
                // Get thumbnail data
                var thumbnailStream = await cvClient.GenerateThumbnailInStreamAsync(100, 100,imageData, true);

                // Save thumbnail image
                string thumbnailFileName = "thumbnail.png";
                using (Stream thumbnailFile = File.Create(thumbnailFileName))
                {
                    thumbnailStream.CopyTo(thumbnailFile);
                }

                Console.WriteLine($"Thumbnail saved in {thumbnailFileName}");
            }
        }

        static void AddImageToFolder(string imageFile, string category){

            string newPath = Path.Combine(Environment.CurrentDirectory, category);
            if(!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);
            string destFile = Path.Combine(newPath, Path.GetFileName(imageFile));
            File.Copy(imageFile, destFile, true);
        }
    }
}
