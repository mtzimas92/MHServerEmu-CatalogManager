using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;



namespace CatalogManager.Services
{
    public class HtmlGeneratorService
    {
        private readonly string _htmlOutputDirectory;
        private readonly string _imageOutputDirectory;
        private readonly string _cssOutputDirectory;
        private readonly Dictionary<string, string> _displayNameMapping;

        private readonly CatalogService _catalogService;

        public HtmlGeneratorService(CatalogService catalogService = null, string outputDirectory = "WebContent")
        {
            _catalogService = catalogService;
            _htmlOutputDirectory = Path.Combine(outputDirectory, "html");
            _imageOutputDirectory = Path.Combine(outputDirectory, "images");
            _cssOutputDirectory = Path.Combine(outputDirectory, "css");

            // Ensure directories exist
            Directory.CreateDirectory(_htmlOutputDirectory);
            Directory.CreateDirectory(_imageOutputDirectory);
            Directory.CreateDirectory(_cssOutputDirectory);

            // Create CSS file if it doesn't exist
            CreateCssFileIfNeeded();
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "display_names.json");
            var jsonContent = File.ReadAllText(jsonPath);
            _displayNameMapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

        }
        private string GetItemName(ulong prototypeId)
        {
            var path = MHServerEmu.Games.GameData.GameDatabase.GetPrototypeName((MHServerEmu.Games.GameData.PrototypeId)prototypeId);
            if (_displayNameMapping.TryGetValue(path, out string displayName) && displayName != "N/A")
            {
                return displayName;
            }
            return path;
        }
        private void CreateCssFileIfNeeded()
        {
            string cssFilePath = Path.Combine(_cssOutputDirectory, "style.css");
            if (!File.Exists(cssFilePath))
            {
                string css = @"html {
    background-color: #0d0d0b;
    color: #d7d7d7;
    font: 18px/1.385 Verdana;
}

*:focus {
    outline-color: #00e8ff;
}

h1, h2, h3, h4, h5, h6 {
    color: #00aaff;
    font-family: ""Bebas Neue"", ""Trebuchet MS"", Verdana, sans-serif;
    font-weight: normal;
}

button, input {
    box-sizing: border-box;
    border: 1px solid black;
    box-shadow: 0 0 1px 1px #00aaff;
    background: #1c1c1c;
    color: inherit;
    font: inherit;
}

button {
    padding: 8px;
}

button:hover {
    box-shadow: 0 0 1px 1px white;
}

div.content {
    margin: 10px auto;
    padding: 20px;
    border: 1px solid #00717c;
    background-color: #00090e;
}

div.buttons {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

span.enhanced-keyword {
    color: #9128bb;
}

span.price {
    color: #00aaff;
    font-family: ""Bebas Neue"", ""Trebuchet MS"", Verdana, sans-serif;
    font-weight: normal;
    font-size: 32px;
}
";
                
                File.WriteAllText(cssFilePath, css);
            }
        }
        
        public async Task<string> GenerateBundleHtmlAsync(CatalogEntry bundle, bool saveToFile = true)
        {
            if (bundle == null)
                throw new ArgumentNullException(nameof(bundle));
                
            if (bundle.LocalizedEntries == null || bundle.LocalizedEntries.Count == 0)
                throw new ArgumentException("Bundle must have at least one localized entry");
                
            var entry = bundle.LocalizedEntries[0];
            string title = entry.Title;
            int price = entry.ItemPrice;
            
            // Build items list HTML
            var itemsHtml = new StringBuilder();
            foreach (var guidItem in bundle.GuidItems)
            {
                string itemName = GetItemName(guidItem.ItemPrototypeRuntimeIdForClient);
                
                // Do not specify quantity if it's only one thing
                if (guidItem.Quantity == 1)
                {
                    itemsHtml.AppendLine($"\t\t\t<li>{itemName}</li>");
                }
                else
                {
                    itemsHtml.AppendLine($"\t\t\t<li>{itemName} x{guidItem.Quantity}</li>");
                }
            }
            
            // Format SkuId as hexadecimal (0x format)
            string skuIdHex = $"0x{bundle.SkuId:X}";
            
            // Generate HTML using the simpler template
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("\t<meta charset=\"UTF-8\">");
            html.AppendLine("\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"\t<title>{title}</title>");
            html.AppendLine("\t<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
            html.AppendLine("\t<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
            html.AppendLine("\t<link href=\"https://fonts.googleapis.com/css2?family=Bebas+Neue&display=swap\" rel=\"stylesheet\">");
            html.AppendLine("\t<link rel=\"stylesheet\" href=\"css/style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("\t<div class=\"content\">");
            html.AppendLine($"\t\t<h1>{title}</h1>");
            html.AppendLine("\t\t<p>Included in this bundle:</p>");
            html.AppendLine("\t\t<ul>");
            html.Append(itemsHtml.ToString());
            html.AppendLine("\t\t</ul>");
            html.AppendLine("\t</div>");
            html.AppendLine("\t<div class=\"buttons content\">");
            html.AppendLine($"\t\t<span class=\"price\">{price} G</span>");
            html.AppendLine($"\t\t<button onclick=\"myApi.BuyBundleFromJS('{skuIdHex}')\">Buy Now!</button>");
            html.AppendLine("\t</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            string htmlContent = html.ToString();
            
            if (saveToFile)
            {
                // Create a filename based on the bundle title
                string fileName = $"{title.ToLower().Replace(" ", "_")}_en_bundle.html";
                string filePath = Path.Combine(_htmlOutputDirectory, fileName);
                
                // Save the HTML file
                await File.WriteAllTextAsync(filePath, htmlContent);
                
                return filePath;
            }
            
            return htmlContent;
        }
        
        public async Task<string> GenerateThumbnailImageAsync(string title)
        {
            // Generate a simple placeholder image
            string fileName = $"MTX_Store_Bundle_{title.Replace(" ", "-")}_Thumb.png";
            string filePath = Path.Combine(_imageOutputDirectory, fileName);
            
            // Generate a placeholder image
            await GeneratePlaceholderImageAsync(filePath, title);
            
            return filePath;
        }
        
        private async Task GeneratePlaceholderImageAsync(string filePath, string title)
        {
            // Create a more attractive placeholder image with the bundle title
            int width = 256;
            int height = 128;
            
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Draw gradient background
                var gradientBrush = new LinearGradientBrush(
                    Color.FromRgb(26, 41, 128),  // Dark blue
                    Color.FromRgb(38, 83, 156),  // Medium blue
                    new Point(0, 0),
                    new Point(1, 1));
                
                dc.DrawRectangle(gradientBrush, null, new System.Windows.Rect(0, 0, width, height));
                
                // Draw a subtle pattern
                for (int i = 0; i < 10; i++)
                {
                    var pen = new Pen(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 1);
                    dc.DrawLine(pen, new Point(0, i * 15), new Point(width, i * 15));
                }
                
                // Draw a border
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 2), 
                    new System.Windows.Rect(2, 2, width - 4, height - 4));
                
                // Draw "BUNDLE" text at the top
                var bundleText = new FormattedText(
                    "BUNDLE",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    14,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                
                dc.DrawText(bundleText, new System.Windows.Point((width - bundleText.Width) / 2, 10));
                
                // Draw main title text
                var titleText = new FormattedText(
                    title,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    18,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                
                // Add a shadow effect
                var shadowText = new FormattedText(
                    title,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    18,
                    new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                
                dc.DrawText(shadowText, new System.Windows.Point((width - titleText.Width) / 2 + 2, (height - titleText.Height) / 2 + 2));
                dc.DrawText(titleText, new System.Windows.Point((width - titleText.Width) / 2, (height - titleText.Height) / 2));
                
                // Draw "EXCLUSIVE" tag if title contains certain keywords
                if (title.Contains("Exclusive", StringComparison.OrdinalIgnoreCase) || 
                    title.Contains("Limited", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Special", StringComparison.OrdinalIgnoreCase))
                {
                    // Draw a red badge in the corner
                    var exclusiveBrush = new SolidColorBrush(Color.FromRgb(230, 57, 70));
                    var exclusiveRect = new System.Windows.Rect(width - 80, 10, 70, 25);
                    dc.DrawRectangle(exclusiveBrush, null, exclusiveRect);
                    
                    var exclusiveText = new FormattedText(
                        "EXCLUSIVE",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        10,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                    
                    dc.DrawText(exclusiveText, 
                        new System.Windows.Point(width - 75 + (70 - exclusiveText.Width) / 2, 10 + (25 - exclusiveText.Height) / 2));
                }
            }
            
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            
            using (var stream = File.Create(filePath))
            {
                encoder.Save(stream);
            }
            
            await Task.CompletedTask; // Just to make the method async
        }
        
        
        private string GetItemCategory(ulong prototypeId)
        {
            var path = MHServerEmu.Games.GameData.GameDatabase.GetPrototypeName((MHServerEmu.Games.GameData.PrototypeId)prototypeId);
            
            if (path.Contains("/Consumables/")) return "Consumables";
            if (path.Contains("/CharacterTokens/")) return "Character Tokens";
            if (path.Contains("/Costumes/")) return "Costumes";
            if (path.Contains("/CurrencyItems/")) return "Currency Items";
            if (path.Contains("/Pets/")) return "Pets";
            if (path.Contains("/Crafting/")) return "Crafting";
            if (path.Contains("/StashInventories/PageProtos/AvatarGear")) return "Stash Tabs";
            if (path.Contains("/Test/") || path.Contains("/RaidTest/") || path.Contains("/TestMedals/")) return "Test Gear";
            
            return "Other";
        }
        
        public async Task<string> GenerateItemDetailsHtmlAsync(ulong prototypeId, string title, string description, int price)
        {
            // Generate a simple HTML page for a single item
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>{title} - Marvel Heroes Store</title>");
            html.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Roboto:wght@400;700&display=swap\" rel=\"stylesheet\">");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"../css/bundle-style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class=\"container\">");
            
            // Header with title
            html.AppendLine("        <div class=\"header\">");
            html.AppendLine("            <div class=\"header-content\">");
            html.AppendLine($"                <h1>{title}</h1>");
            html.AppendLine($"                <div class=\"price\">{price} G</div>");
            html.AppendLine($"                <a href=\"#\" class=\"buy-button\">Purchase Now</a>");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            
            // Description
            html.AppendLine($"        <div class=\"description\">{description}</div>");
            
            // Item details
            html.AppendLine("        <h2>Item Details:</h2>");
            html.AppendLine("        <div class=\"item\">");
            html.AppendLine($"            <div class=\"item-name\">{GetItemName(prototypeId)}</div>");
            html.AppendLine("        </div>");
            
            // Footer
            html.AppendLine("        <div class=\"footer\">");
            html.AppendLine("            <p>© Marvel Heroes. All rights reserved.</p>");
            html.AppendLine("        </div>");
            
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
    }
}
