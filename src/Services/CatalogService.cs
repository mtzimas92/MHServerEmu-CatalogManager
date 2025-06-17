using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json.Serialization;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;

namespace CatalogManager.Services
{
    public class CatalogService
    {
        private readonly string _catalogPath;
        private readonly string _patchPath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _catalogLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, List<string>> _categoryModifierMappings = new();
        private readonly HashSet<ulong> _patchFileSkuIds = new();
        
        // Cache for better performance
        private List<CatalogEntry> _cachedCatalogItems;
        private HashSet<string> _cachedCategories;
        private DateTime _lastCatalogLoadTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(5);
        
        // Prototype cache
        private readonly Dictionary<ulong, string> _prototypeNameCache = new();

        public CatalogService(string dataDirectory = "Data")
        {
            _catalogPath = Path.Combine(dataDirectory, "Catalog.json");
            _patchPath = Path.Combine(dataDirectory, "CatalogPatch.json");
            
            _jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            
            // Ensure data directory exists
            Directory.CreateDirectory(dataDirectory);
            
            // Create empty patch file if it doesn't exist
            if (!File.Exists(_patchPath))
            {
                File.WriteAllText(_patchPath, "[]");
            }
            
            // Initialize category modifiers
            Task.Run(InitializeCategoryModifierMappingsAsync);
        }
        private async Task InitializeCategoryModifierMappingsAsync()
        {
            try
            {
                var catalogItems = await GetItemsAsync("All", "");
                
                lock (_categoryModifierMappings)
                {
                    _categoryModifierMappings.Clear();
                    
                    foreach (var group in catalogItems.GroupBy(item => item.Type.Name))
                    {
                        var modifiers = group
                            .SelectMany(item => item.TypeModifiers)
                            .Select(m => m.Name)
                            .Distinct()
                            .ToList();
                            
                        _categoryModifierMappings[group.Key] = modifiers;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing category modifiers: {ex.Message}");
            }
        }

        public async Task<(List<CatalogEntry> Items, HashSet<string> Categories)> LoadCatalogAsync(bool forceRefresh = false)
        {
            // Return cached data if it's still valid
            if (!forceRefresh && 
                _cachedCatalogItems != null && 
                _cachedCategories != null && 
                (DateTime.Now - _lastCatalogLoadTime) < _cacheExpirationTime)
            {
                return (_cachedCatalogItems, _cachedCategories);
            }
            
            await _catalogLock.WaitAsync();
            try
            {
                var items = new List<CatalogEntry>();
                _patchFileSkuIds.Clear();

                // Load patch file
                if (File.Exists(_patchPath))
                {
                    var patchJson = await File.ReadAllTextAsync(_patchPath);
                    var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);
                    
                    if (patch != null) 
                    {
                        items.AddRange(patch);
                        _patchFileSkuIds.UnionWith(patch.Select(x => x.SkuId));
                    }
                }

                // Load base catalog if it exists
                if (File.Exists(_catalogPath))
                {
                    var catalogJson = await File.ReadAllTextAsync(_catalogPath);
                    var catalogRoot = JsonSerializer.Deserialize<CatalogRoot>(catalogJson, _jsonOptions);
                    
                    if (catalogRoot?.Entries != null)
                    {
                        items.AddRange(catalogRoot.Entries);
                    }
                }

                // Get unique categories from all items
                var categories = new HashSet<string>(items.Select(i => i.Type.Name).OrderBy(n => n));
                
                // Update cache
                _cachedCatalogItems = items;
                _cachedCategories = categories;
                _lastCatalogLoadTime = DateTime.Now;
                
                return (items, categories);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading catalog: {ex.Message}");
                throw;
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        public bool IsItemFromPatch(ulong skuId) => _patchFileSkuIds.Contains(skuId);

        public async Task<bool> SaveItemAsync(CatalogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
                        
            await _catalogLock.WaitAsync();
            try
            {
                // Determine if this is a new item or an update to an existing item
                bool isNewItem = !IsItemInCatalog(entry.SkuId);
                
                if (isNewItem || IsItemFromPatch(entry.SkuId))
                {
                    // Save to patch file
                    await SaveToPatchFileAsync(entry);
                    return true;
                }
                else
                {
                    // Update in main catalog in-place without reformatting
                    return await UpdateInMainCatalogInPlaceAsync(entry);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving item {entry.SkuId}: {ex.Message}");
                throw;
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        private async Task SaveToPatchFileAsync(CatalogEntry entry)
        {
            // Load current patch file
            var patchJson = await File.ReadAllTextAsync(_patchPath);
            var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions) ?? new List<CatalogEntry>();
            
            // Remove existing entry with same SKU if it exists
            patch.RemoveAll(x => x.SkuId == entry.SkuId);
            
            // Add the new/updated entry
            patch.Add(entry);
            
            // Save back to file with backup
            string backupPath = _patchPath + ".bak";
            if (File.Exists(_patchPath))
            {
                File.Copy(_patchPath, backupPath, true);
            }
            
            await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
            
            // Update patch SKU cache
            _patchFileSkuIds.Add(entry.SkuId);
        }
        
        private async Task<bool> UpdateInMainCatalogInPlaceAsync(CatalogEntry entry)
        {
            if (!File.Exists(_catalogPath))
            {
                throw new FileNotFoundException("Base catalog file not found", _catalogPath);
            }

            // Create backup
            string backupPath = _catalogPath + ".bak";
            File.Copy(_catalogPath, backupPath, true);

            try
            {
                // Read the catalog
                string catalogJson = await File.ReadAllTextAsync(_catalogPath);
                var catalog = JsonSerializer.Deserialize<CatalogRoot>(catalogJson, _jsonOptions);

                // Find and update the entry
                var existingEntry = catalog.Entries.FirstOrDefault(e => e.SkuId == entry.SkuId);
                if (existingEntry != null)
                {
                    int index = catalog.Entries.IndexOf(existingEntry);
                    catalog.Entries[index] = entry;

                    // Write back to file using the same options
                    await File.WriteAllTextAsync(_catalogPath, JsonSerializer.Serialize(catalog, _jsonOptions));
            
                    // Invalidate cache
                    _lastCatalogLoadTime = DateTime.MinValue;
            
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating catalog in place: {ex.Message}");
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, _catalogPath, true);
                }
                throw;
            }
        }
        
        private bool IsItemInCatalog(ulong skuId)
        {
            return _cachedCatalogItems?.Any(i => i.SkuId == skuId) ?? false;
        }

        public async Task<bool> DeleteFromPatchAsync(ulong skuId)
        {
            await _catalogLock.WaitAsync();
            try
            {
                if (!File.Exists(_patchPath))
                {
                    return false;
                }
                
                // Create backup
                string backupPath = _patchPath + ".bak";
                File.Copy(_patchPath, backupPath, true);
                
                try
                {
                    var patchJson = await File.ReadAllTextAsync(_patchPath);
                    var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);
                    
                    if (patch == null)
                    {
                        return false;
                    }
                    
                    int initialCount = patch.Count;
                    patch.RemoveAll(x => x.SkuId == skuId);
                    
                    if (patch.Count < initialCount)
                    {
                        await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
                        _patchFileSkuIds.Remove(skuId);
                        
                        // Invalidate cache
                        _lastCatalogLoadTime = DateTime.MinValue;
                        return true;
                    }
                    return false;
                }
                catch
                {
                    // Restore from backup on error
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, _patchPath, true);
                    }
                    throw;
                }
            }
            finally
            {
                _catalogLock.Release();
            }
        }
        public async Task<bool> DeleteFromCatalogAsync(ulong skuId)
        {
            await _catalogLock.WaitAsync();
            try
            {
                bool success = false;

                // Handle catalog.json deletion
                string catalogJson = await File.ReadAllTextAsync(_catalogPath);
                var catalog = JsonSerializer.Deserialize<CatalogRoot>(catalogJson);

                if (catalog?.Entries != null)
                {
                    string backupPath = _catalogPath + ".bak";
                    File.Copy(_catalogPath, backupPath, true);

                    try
                    {
                        int initialCount = catalog.Entries.Count;
                        catalog.Entries.RemoveAll(x => x.SkuId == skuId);

                        if (catalog.Entries.Count < initialCount)
                        {
                            var options = new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                            };

                            var updatedCatalog = new CatalogRoot
                            {
                                TimestampSeconds = catalog.TimestampSeconds,
                                TimestampMicroseconds = catalog.TimestampMicroseconds,
                                Entries = catalog.Entries,
                                Urls = catalog.Urls,
                                ClientMustDownloadImages = catalog.ClientMustDownloadImages
                            };

                            await File.WriteAllTextAsync(_catalogPath, JsonSerializer.Serialize(updatedCatalog, options));
                            success = true;
                        }
                    }
                    catch
                    {
                        if (File.Exists(backupPath))
                            File.Copy(backupPath, _catalogPath, true);
                        throw;
                    }
                }

                // Handle patch file deletion
                if (File.Exists(_patchPath))
                {
                    string patchBackupPath = _patchPath + ".bak";
                    File.Copy(_patchPath, patchBackupPath, true);

                    try
                    {
                        var patchJson = await File.ReadAllTextAsync(_patchPath);
                        var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions);

                        if (patch != null)
                        {
                            int initialCount = patch.Count;
                            patch.RemoveAll(x => x.SkuId == skuId);

                            if (patch.Count < initialCount)
                            {
                                await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
                                _patchFileSkuIds.Remove(skuId);
                                success = true;
                            }
                        }
                    }
                    catch
                    {
                        if (File.Exists(patchBackupPath))
                            File.Copy(patchBackupPath, _patchPath, true);
                        throw;
                    }
                }

                if (success)
                {
                    _lastCatalogLoadTime = DateTime.MinValue;
                }

                return success;
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        public List<string> GetCategoryModifiers(string category)
        {
            lock (_categoryModifierMappings)
            {
                return _categoryModifierMappings.TryGetValue(category, out var modifiers) 
                    ? new List<string>(modifiers) // Return a copy to prevent modification
                    : new List<string>();
            }
        }

        public async Task<ulong> GetNextAvailableSkuId()
        {
            var (items, _) = await LoadCatalogAsync();
            return items.Count > 0 ? items.Max(i => i.SkuId) + 1 : 1000;
        }

        public async Task<IEnumerable<CatalogEntry>> GetItemsAsync(
            string category, 
            string searchText, 
            CancellationToken cancellationToken = default,
            int? minPrice = null, 
            int? maxPrice = null)
        {
            var (items, _) = await LoadCatalogAsync();
            
            // Use more efficient LINQ with query termination
            return items.AsParallel()
                .WithCancellation(cancellationToken)
                .Where(item => 
                    (category == "All" || item.Type.Name == category) &&
                    (string.IsNullOrEmpty(searchText) || 
                        item.LocalizedEntries.Any(e => e.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                        item.SkuId.ToString().Contains(searchText) ||
                        item.GuidItems.Any(g => g.ItemPrototypeRuntimeIdForClient.ToString().Contains(searchText))) &&
                    (!minPrice.HasValue || item.LocalizedEntries.Any(e => e.ItemPrice >= minPrice.Value)) &&
                    (!maxPrice.HasValue || item.LocalizedEntries.Any(e => e.ItemPrice <= maxPrice.Value))
                )
                .ToList();
        }
        
        // Helper method to get prototype name with caching
        public string GetPrototypeName(ulong prototypeId)
        {
            if (_prototypeNameCache.TryGetValue(prototypeId, out string name))
            {
                return name;
            }
            
            name = GameDatabase.GetPrototypeName((PrototypeId)prototypeId);
            _prototypeNameCache[prototypeId] = name;
            return name;
        }

        public async Task<bool> UpdateTypeModifiersAsync(CatalogEntry item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
                
            await _catalogLock.WaitAsync();
            try
            {
                // Determine if this is a patch item or a catalog item
                bool isPatchItem = IsItemFromPatch(item.SkuId);
                
                if (isPatchItem)
                {
                    // For patch items, we need to load the current patch file
                    var patchJson = await File.ReadAllTextAsync(_patchPath);
                    var patch = JsonSerializer.Deserialize<List<CatalogEntry>>(patchJson, _jsonOptions) ?? new List<CatalogEntry>();
                    
                    // Find the existing entry
                    var existingEntry = patch.FirstOrDefault(x => x.SkuId == item.SkuId);
                    if (existingEntry == null) return false;
                    
                    // Only update the TypeModifiers field
                    existingEntry.TypeModifiers = item.TypeModifiers;
                    
                    // Save back to file with backup
                    string backupPath = _patchPath + ".bak";
                    if (File.Exists(_patchPath))
                    {
                        File.Copy(_patchPath, backupPath, true);
                    }
                    
                    await File.WriteAllTextAsync(_patchPath, JsonSerializer.Serialize(patch, _jsonOptions));
                    
                    // Invalidate cache
                    _lastCatalogLoadTime = DateTime.MinValue;
                    
                    return true;
                }
                else
                {
                    // For catalog items, we need to update in-place
                    if (!File.Exists(_catalogPath))
                    {
                        throw new FileNotFoundException("Main catalog file not found", _catalogPath);
                    }
                    
                    // Create backup
                    string backupPath = _catalogPath + ".bak";
                    File.Copy(_catalogPath, backupPath, true);
                    
                    try
                    {
                        // Read the entire file as text
                        string catalogJson = await File.ReadAllTextAsync(_catalogPath);
                        
                        // Find the entry by SkuId
                        string skuIdPattern = $"\"SkuId\":{item.SkuId}";
                        int skuIdIndex = catalogJson.IndexOf(skuIdPattern);
                        
                        if (skuIdIndex < 0)
                        {
                            // Entry not found in the catalog
                            Debug.WriteLine($"Entry with SkuId {item.SkuId} not found in catalog");
                            return false;
                        }
                        
                        // Find the TypeModifiers section
                        string typeModifiersPattern = "\"TypeModifiers\":";
                        int typeModifiersIndex = catalogJson.IndexOf(typeModifiersPattern, skuIdIndex);
                        
                        if (typeModifiersIndex < 0)
                        {
                            // TypeModifiers not found in the entry
                            Debug.WriteLine($"TypeModifiers not found for SkuId {item.SkuId}");
                            return false;
                        }
                        
                        // Find the start and end of the TypeModifiers array
                        int arrayStartIndex = catalogJson.IndexOf('[', typeModifiersIndex);
                        if (arrayStartIndex < 0) return false;
                        
                        // Find the matching closing bracket
                        int bracketCount = 1;
                        int arrayEndIndex = arrayStartIndex + 1;
                        
                        while (bracketCount > 0 && arrayEndIndex < catalogJson.Length)
                        {
                            char c = catalogJson[arrayEndIndex];
                            if (c == '[') bracketCount++;
                            else if (c == ']') bracketCount--;
                            arrayEndIndex++;
                        }
                        
                        if (bracketCount != 0) return false; // Unbalanced brackets
                        
                        // Serialize just the TypeModifiers array
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            NumberHandling = JsonNumberHandling.AllowReadingFromString
                        };
                        
                        string newTypeModifiersJson = JsonSerializer.Serialize(item.TypeModifiers, jsonOptions);
                        
                        // Replace the old TypeModifiers array with the new one
                        string updatedCatalog = catalogJson.Substring(0, arrayStartIndex) + 
                                            newTypeModifiersJson + 
                                            catalogJson.Substring(arrayEndIndex);
                        
                        // Write back to the file
                        await File.WriteAllTextAsync(_catalogPath, updatedCatalog);
                        
                        // Invalidate cache
                        _lastCatalogLoadTime = DateTime.MinValue;
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Restore from backup on error
                        Debug.WriteLine($"Error updating catalog in place: {ex.Message}");
                        if (File.Exists(backupPath))
                        {
                            File.Copy(backupPath, _catalogPath, true);
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating type modifiers: {ex.Message}");
                return false;
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        public async Task<CatalogEntry> GetItemBySkuIdAsync(ulong skuId)
        {
            try
            {
                // Make sure the catalog is loaded
                await LoadCatalogAsync();
        
                // Find the item with the matching SkuId
                return _cachedCatalogItems.FirstOrDefault(item => item.SkuId == skuId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting item by SkuId: {ex.Message}");
                return null;
            }
        }

        public async Task<CatalogEntry> GetItemByPrototypeIdAsync(ulong prototypeId)
        {
            try
            {
                // Make sure the catalog is loaded
                await LoadCatalogAsync();
        
                // Find the item with the matching prototype ID in GuidItems
                return _cachedCatalogItems.FirstOrDefault(item => 
                    item.GuidItems.Any(g => g.ItemPrototypeRuntimeIdForClient == prototypeId));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting item by prototype ID: {ex.Message}");
                return null;
            }
        }
    }

    public class CatalogRoot
    {
        public long TimestampSeconds { get; set; }
        public long TimestampMicroseconds { get; set; }
        public List<CatalogEntry> Entries { get; set; } = new List<CatalogEntry>();
        public List<CatalogUrls> Urls { get; set; } = new List<CatalogUrls>();
        public bool ClientMustDownloadImages { get; set; }
    }

    public class CatalogUrls
    {
        public string LocaleId { get; set; }
        public string StoreHomePageUrl { get; set; }
        public List<StoreBannerPageUrl> StoreBannerPageUrls { get; set; } = new List<StoreBannerPageUrl>();
        public string StoreRealMoneyUrl { get; set; }
    }

    public class StoreBannerPageUrl
    {
        public string Type { get; set; }
        public string Url { get; set; }
    }
}        
