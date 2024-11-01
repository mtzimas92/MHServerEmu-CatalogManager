using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json.Serialization;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
namespace CatalogManager.Services
{
    public class CatalogService
    {
        private readonly string _catalogPath = Path.Combine("Data", "Catalog.json");
        private readonly string _patchPath = Path.Combine("Data", "CatalogPatch.json");
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        private Dictionary<string, List<string>> _categoryModifierMappings;

        private HashSet<ulong> _patchFileSkuIds = new();

        public async Task<(List<CatalogEntry> Items, HashSet<string> Categories)> LoadCatalogAsync()
        {
            //Debug.WriteLine("Loading catalog files...");

            var items = new List<CatalogEntry>();

            // Load patch file
            var patchJson = await File.ReadAllTextAsync(_patchPath);
            var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);
            //Debug.WriteLine($"Loaded {patch?.Count ?? 0} items from patch file");
            if (patch != null) 
            {
                items.AddRange(patch);
                _patchFileSkuIds = new HashSet<ulong>(patch.Select(x => x.SkuId));
            }

            // Load base catalog if it exists
            if (File.Exists(_catalogPath))
            {
                var catalogJson = await File.ReadAllTextAsync(_catalogPath);
                var catalogRoot = JsonSerializer.Deserialize<CatalogRoot>(catalogJson, _jsonOptions);
                if (catalogRoot?.Entries != null) items.AddRange(catalogRoot.Entries);
            }

            // Get unique categories from all items
            var categories = new HashSet<string>(items.Select(i => i.Type.Name).OrderBy(n => n));
            //Debug.WriteLine($"Loaded {items.Count} items and {categories.Count} categories");
            return (items, categories);
        }
        public CatalogService()
        {
            InitializeCategoryModifierMappings();
        }
/*         public async void AnalyzeDesignStates()
        {
            var (catalogItems, _) = await LoadCatalogAsync();

            var pathAnalysis = catalogItems
                .Select(item => new
                {
                    Path = Path.GetDirectoryName(GameDatabase.GetPrototypeName((PrototypeId)item.GuidItems[0].ItemPrototypeRuntimeIdForClient)),
                    Type = item.Type.Name
                })
                .Distinct()
                .OrderBy(x => x.Path)
                .ToList();

            string filePath = Path.Combine(AppContext.BaseDirectory, "CatalogPathAnalysis.json");
            File.WriteAllText(filePath, 
                JsonSerializer.Serialize(pathAnalysis, new JsonSerializerOptions { WriteIndented = true }));
        } */

        private string GetExistingTypeForPath(string path, List<CatalogEntry> catalogItems)
        {
            var existingItem = catalogItems.FirstOrDefault(i => 
                GameDatabase.GetPrototypeName((PrototypeId)i.GuidItems[0].ItemPrototypeRuntimeIdForClient) == path);
            return existingItem?.Type.Name ?? "Unknown";
        }
        public bool IsItemFromPatch(ulong skuId) => _patchFileSkuIds.Contains(skuId);

        public async Task SaveItemAsync(CatalogEntry entry)
        {
            var catalogJson = await File.ReadAllTextAsync(_catalogPath);
            
            if (catalogJson.Contains($"\"SkuId\":{entry.SkuId}"))
            {
                var doc = JsonDocument.Parse(catalogJson);
                var entries = doc.RootElement.GetProperty("Entries");
                
                int startIndex = -1;
                int endIndex = -1;
                
                // Find the exact position of this entry in the JSON string
                for (int i = 0; i < entries.GetArrayLength(); i++)
                {
                    if (entries[i].GetProperty("SkuId").GetUInt64() == entry.SkuId)
                    {
                        var entryStr = entries[i].ToString();
                        startIndex = catalogJson.IndexOf(entryStr);
                        endIndex = startIndex + entryStr.Length;
                        break;
                    }
                }
                
                if (startIndex >= 0)
                {
                    // Create the new entry JSON
                    var newEntryJson = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
                    
                    // Replace only this specific entry
                    var updatedJson = catalogJson.Substring(0, startIndex) + 
                                    newEntryJson + 
                                    catalogJson.Substring(endIndex);
                    
                    await File.WriteAllTextAsync(_catalogPath, updatedJson);
                }
            }
            else
            {
                var patchJson = await File.ReadAllTextAsync(_patchPath);
                var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);
                patch.Add(entry);
                await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
            }
        }

        private async void InitializeCategoryModifierMappings()
        {
            var catalogItems = await GetItemsAsync("All", "");
            _categoryModifierMappings = catalogItems
                .GroupBy(item => item.Type.Name)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(item => item.TypeModifiers)
                        .Select(m => m.Name)
                        .Distinct()
                        .ToList()
                );
        }
        public List<string> GetCategoryModifiers(string category)
        {
            return _categoryModifierMappings.TryGetValue(category, out var modifiers) 
                ? modifiers 
                : new List<string>();
        }
        public async Task<ulong> GetNextAvailableSkuId()
        {
            var (items, _) = await LoadCatalogAsync();
            return items.Max(i => i.SkuId) + 1;
        }
/*         public async Task AnalyzeItemCategories()
        {
            var catalogItems = await GetItemsAsync("All", "");
            
            // Analyze existing catalog mappings
            var catalogMappings = catalogItems
                .GroupBy(item => GameDatabase.GetPrototypeName((PrototypeId)item.GuidItems[0].ItemPrototypeRuntimeIdForClient))
                .Select(g => new
                {
                    Path = g.Key,
                    Type = g.First().Type.Name,
                    Modifiers = g.First().TypeModifiers.Select(m => m.Name)
                })
                .ToList();

            // Get all items from game database
            var allGameItems = GameDatabase.DataDirectory
                .IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.NoAbstractApprovedOnly)
                .Select(protoId => GameDatabase.GetPrototypeName(protoId))
                .ToList();

            // Find items not in our current path filters
            var currentPaths = new[]
            {
                "Entity/Items/Consumables",
                "Entity/Items/CharacterTokens",
                "Entity/Items/Costumes",
                "Entity/Items/CurrencyItems",
                "Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear"
            };

            var unmappedItems = allGameItems
                .Where(path => !currentPaths.Any(filter => path.StartsWith(filter)))
                .Select(path => new
                {
                    Path = path,
                    ExistsInCatalog = catalogItems.Any(c => GameDatabase.GetPrototypeName((PrototypeId)c.GuidItems[0].ItemPrototypeRuntimeIdForClient) == path)
                })
                .Where(item => item.ExistsInCatalog)
                .ToList();

            var analysis = new
            {
                CatalogMappings = catalogMappings,
                UnmappedItems = unmappedItems
            };

            string filePath = Path.Combine(AppContext.BaseDirectory, "ItemCategoryAnalysis.json");
            await File.WriteAllTextAsync(filePath, 
                JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));
        } */
        public async Task<IEnumerable<CatalogEntry>> GetItemsAsync(
            string category, 
            string searchText, 
            int? minPrice = null, 
            int? maxPrice = null)        
        {
            var (items, _) = await LoadCatalogAsync();
            var filtered = items.Where(item => 
                (category == "All" || item.Type.Name == category) &&
                (string.IsNullOrEmpty(searchText) || 
                item.LocalizedEntries[0].Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.SkuId.ToString().Contains(searchText) ||
                item.GuidItems[0].ItemPrototypeRuntimeIdForClient.ToString().Contains(searchText)) &&
                (!minPrice.HasValue || item.LocalizedEntries[0].ItemPrice >= minPrice.Value) &&
                (!maxPrice.HasValue || item.LocalizedEntries[0].ItemPrice <= maxPrice.Value)
            );
            return filtered;
        }

        public async Task DeleteFromPatchAsync(ulong skuId)
        {
            var patchJson = await File.ReadAllTextAsync(_patchPath);
            var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);

            patch.RemoveAll(x => x.SkuId == skuId);
            await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
        }
    }

    public class CatalogRoot
    {
        public long TimestampSeconds { get; set; }
        public long TimestampMicroseconds { get; set; }
        public List<CatalogEntry> Entries { get; set; }
    }
}
