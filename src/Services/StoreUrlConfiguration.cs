using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatalogManager.Services
{
    public class StoreUrlConfiguration
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "store_config.json");
        
        public string BundleBaseUrl { get; set; } = "http://localhost/store/bundles";
        public string ImageBaseUrl { get; set; } = "http://localhost/store/images";
        
        public static async Task<StoreUrlConfiguration> LoadAsync()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = await File.ReadAllTextAsync(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<StoreUrlConfiguration>(json);
                    return config ?? new StoreUrlConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading URL configuration: {ex.Message}");
            }
            
            return new StoreUrlConfiguration();
        }
        
        public async Task SaveAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving URL configuration: {ex.Message}");
            }
        }
    }
}