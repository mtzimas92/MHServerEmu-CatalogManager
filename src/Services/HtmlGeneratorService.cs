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
        }
        
        private void CreateCssFileIfNeeded()
        {
            string cssFilePath = Path.Combine(_cssOutputDirectory, "bundle-style.css");
            if (!File.Exists(cssFilePath))
            {
                string css = @"
                    body {
                        font-family: 'Roboto', Arial, sans-serif;
                        margin: 0;
                        padding: 0;
                        background-color: #1a1a1a;
                        color: #f0f0f0;
                    }
                    
                    .container {
                        max-width: 1000px;
                        margin: 0 auto;
                        padding: 30px;
                        background-color: #2a2a2a;
                        box-shadow: 0 0 20px rgba(0,0,0,0.5);
                        border-radius: 8px;
                        margin-top: 30px;
                        margin-bottom: 30px;
                    }
                    
                    .header {
                        display: flex;
                        align-items: center;
                        margin-bottom: 30px;
                        border-bottom: 1px solid #444;
                        padding-bottom: 20px;
                    }
                    
                    .header-image {
                        width: 256px;
                        height: 128px;
                        margin-right: 30px;
                        border-radius: 5px;
                        box-shadow: 0 0 10px rgba(0,0,0,0.3);
                    }
                    
                    .header-content h1 {
                        color: #e63946;
                        margin: 0 0 10px 0;
                        font-size: 32px;
                    }
                    
                    .price {
                        font-size: 28px;
                        font-weight: bold;
                        color: #ffd700;
                        margin: 10px 0;
                    }
                    
                    .description {
                        margin-bottom: 30px;
                        font-size: 16px;
                        line-height: 1.6;
                        color: #cccccc;
                    }
                    
                    .items-container {
                        display: grid;
                        grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
                        gap: 20px;
                        margin-top: 30px;
                    }
                    
                    .item {
                        background-color: #333;
                        border-radius: 8px;
                        padding: 15px;
                        transition: transform 0.2s, box-shadow 0.2s;
                        box-shadow: 0 4px 6px rgba(0,0,0,0.1);
                    }
                    
                    .item:hover {
                        transform: translateY(-5px);
                        box-shadow: 0 8px 15px rgba(0,0,0,0.2);
                    }
                    
                    .item-name {
                        font-weight: bold;
                        font-size: 18px;
                        margin-bottom: 10px;
                        color: #4cc9f0;
                    }
                    
                    .item-type {
                        font-size: 14px;
                        color: #aaa;
                    }
                    
                    .footer {
                        margin-top: 40px;
                        text-align: center;
                        font-size: 14px;
                        color: #888;
                        border-top: 1px solid #444;
                        padding-top: 20px;
                    }
                    
                    .buy-button {
                        display: inline-block;
                        background-color: #e63946;
                        color: white;
                        padding: 12px 30px;
                        border-radius: 5px;
                        text-decoration: none;
                        font-weight: bold;
                        margin-top: 20px;
                        transition: background-color 0.2s;
                    }
                    
                    .buy-button:hover {
                        background-color: #f25d6a;
                    }
                    
                    .savings {
                        background-color: #4cc9f0;
                        color: #1a1a1a;
                        padding: 5px 10px;
                        border-radius: 5px;
                        font-weight: bold;
                        display: inline-block;
                        margin-left: 15px;
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
            string description = entry.Description;
            int price = entry.ItemPrice;
            
            // Calculate total value of items (if we have access to the catalog service)
            int totalValue = 0;
            int savings = 0;
            string savingsPercentage = "";
            
            if (_catalogService != null)
            {
                try
                {
                    foreach (var guidItem in bundle.GuidItems)
                    {
                        var item = await _catalogService.GetItemByPrototypeIdAsync(guidItem.ItemPrototypeRuntimeIdForClient);
                        if (item != null && item.LocalizedEntries.Count > 0)
                        {
                            totalValue += item.LocalizedEntries[0].ItemPrice;
                        }
                    }
                    
                    if (totalValue > 0)
                    {
                        savings = totalValue - price;
                        savingsPercentage = totalValue > 0 ? $"{(savings * 100 / totalValue):0}%" : "";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error calculating bundle value: {ex.Message}");
                    // Continue without savings information
                }
            }
            
            // Generate thumbnail path
            string thumbnailFileName = $"MTX_Store_Bundle_{title.Replace(" ", "-")}_Thumb.png";
            string thumbnailRelativePath = $"../images/{thumbnailFileName}";
            
            // Generate a simple HTML page for the bundle
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
            
            // Header with image and title
            html.AppendLine("        <div class=\"header\">");
            html.AppendLine($"            <img src=\"{thumbnailRelativePath}\" alt=\"{title}\" class=\"header-image\">");
            html.AppendLine("            <div class=\"header-content\">");
            html.AppendLine($"                <h1>{title}</h1>");
            html.AppendLine($"                <div class=\"price\">{price} G");
            
            // Show savings if available
            if (savings > 0)
            {
                html.AppendLine($"                    <span class=\"savings\">Save {savingsPercentage} ({savings} G)</span>");
            }
            
            html.AppendLine("                </div>");
            html.AppendLine($"                <a href=\"#\" class=\"buy-button\">Purchase Now</a>");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            
            // Description
            html.AppendLine($"        <div class=\"description\">{description}</div>");
            
            // Bundle contents
            html.AppendLine("        <h2>Bundle Contents:</h2>");
            html.AppendLine("        <div class=\"items-container\">");
            
            // Add items in the bundle
            foreach (var guidItem in bundle.GuidItems)
            {
                string itemName = GetItemName(guidItem.ItemPrototypeRuntimeIdForClient);
                
                html.AppendLine("            <div class=\"item\">");
                html.AppendLine($"                <div class=\"item-name\">{itemName}</div>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
                        
            html.AppendLine("    </div>");
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
        
        private string GetItemName(ulong prototypeId)
        {
            // Try to get the item name from the game database
            try
            {
                return MHServerEmu.Games.GameData.GameDatabase.GetPrototypeName((MHServerEmu.Games.GameData.PrototypeId)prototypeId);
            }
            catch
            {
                return $"Item #{prototypeId}";
            }
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
