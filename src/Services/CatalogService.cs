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
        private readonly List<string> _loadedFiles = new();
        private readonly Dictionary<ulong, string> _skuToFileMapping = new(); // Track which file each SKU came from
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _catalogLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, List<string>> _categoryModifierMappings = new();
        private readonly HashSet<ulong> _patchFileSkuIds = new();
        
        // Cache for better performance
        private List<CatalogEntry>? _cachedCatalogItems;
        private HashSet<string>? _cachedCategories;
        private DateTime _lastCatalogLoadTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(5);
        
        // Prototype cache
        private readonly Dictionary<ulong, string> _prototypeNameCache = new();

        // Predefined type modifiers for each category - all possible modifiers from Marvel Heroes
        private static readonly Dictionary<string, List<string>> PredefinedModifiers = new()
        {
            { "Bundle", new List<string> { "Giftable", "NoDisplay", "NoDisplayStore"} },
            { "Hero", new List<string> { "Giftable", "NoDisplay", "NoDisplayStore","Special" } },
            { "Costume", new List<string> { "Giftable", "NoDisplay", "NoDisplayStore","Special" } },
            { "TeamUp", new List<string> { "Giftable", "NoDisplay", "NoDisplayStore","Special" } },
            { "Boost", new List<string> {"Giftable", "NoDisplay", "NoDisplayStore","Special" } },
            { "Chest", new List<string> { "Giftable", "NoDisplay", "NoDisplayStore","Special" } },
            { "Service", new List<string> { "StashPage", "PowerSpecPanel" } }
        };

        public IReadOnlyList<string> LoadedFiles => _loadedFiles.AsReadOnly();

        public CatalogService()
        {
            _jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
        }

        public async Task<bool> LoadCatalogFileAsync(string catalogFilePath)
        {
            // Logger.Log($"LoadCatalogFileAsync: Called with path: {catalogFilePath}");
            
            if (string.IsNullOrEmpty(catalogFilePath))
            {
                // Logger.Log("LoadCatalogFileAsync: Path is null or empty");
                return false;
            }
            
            if (!File.Exists(catalogFilePath))
            {
                // Logger.Log($"LoadCatalogFileAsync: File does not exist: {catalogFilePath}");
                return false;
            }

            // Logger.Log("LoadCatalogFileAsync: Waiting for catalog lock");
            await _catalogLock.WaitAsync();
            try
            {
                // Add to loaded files list if not already present
                if (!_loadedFiles.Contains(catalogFilePath))
                {
                    // Logger.Log($"LoadCatalogFileAsync: Adding file to loaded files: {catalogFilePath}");
                    _loadedFiles.Add(catalogFilePath);
                }
                else
                {
                    // Logger.Log($"LoadCatalogFileAsync: File already loaded: {catalogFilePath}");
                }
                
                _lastCatalogLoadTime = DateTime.MinValue; // Force reload
                
                // Logger.Log("LoadCatalogFileAsync: Calling LoadCatalogInternalAsync");
                var result = await LoadCatalogInternalAsync();
                // Logger.Log($"LoadCatalogFileAsync: LoadCatalogInternalAsync returned {result.Items?.Count ?? 0} items and {result.Categories?.Count ?? 0} categories");
                
                // Initialize category modifiers after loading
                // Logger.Log("LoadCatalogFileAsync: Initializing category modifier mappings");
                await InitializeCategoryModifierMappingsAsync();
                // Logger.Log("LoadCatalogFileAsync: Category modifier mappings initialized");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException("LoadCatalogFileAsync: Exception", ex);
                throw;
            }
            finally
            {
                // Logger.Log("LoadCatalogFileAsync: Releasing catalog lock");
                _catalogLock.Release();
            }
        }

        public async Task ClearAllFilesAsync()
        {
            await _catalogLock.WaitAsync();
            try
            {
                // Logger.Log("ClearAllFilesAsync: Clearing all loaded files");
                _loadedFiles.Clear();
                _skuToFileMapping.Clear();
                _patchFileSkuIds.Clear();
                _cachedCatalogItems = null;
                _cachedCategories = null;
                _lastCatalogLoadTime = DateTime.MinValue;
                // Logger.Log("ClearAllFilesAsync: All files cleared");
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        private async Task InitializeCategoryModifierMappingsAsync()
        {
            try
            {
                var catalogItems = await GetItemsAsync("All", "");
                
                lock (_categoryModifierMappings)
                {
                    _categoryModifierMappings.Clear();
                    
                    // Start with predefined modifiers for each category
                    foreach (var kvp in PredefinedModifiers)
                    {
                        _categoryModifierMappings[kvp.Key] = new List<string>(kvp.Value);
                    }
                    
                    // Merge with modifiers found in the catalog (add any new ones not in predefined list)
                    foreach (var group in catalogItems.GroupBy(item => item.Type.Name))
                    {
                        var catalogModifiers = group
                            .SelectMany(item => item.TypeModifiers)
                            .Select(m => m.Name)
                            .Distinct()
                            .ToList();
                        
                        if (_categoryModifierMappings.ContainsKey(group.Key))
                        {
                            // Add any catalog modifiers not already in the predefined list
                            foreach (var modifier in catalogModifiers)
                            {
                                if (!_categoryModifierMappings[group.Key].Contains(modifier))
                                {
                                    _categoryModifierMappings[group.Key].Add(modifier);
                                }
                            }
                        }
                        else
                        {
                            // New category not in predefined list, add all its modifiers
                            _categoryModifierMappings[group.Key] = catalogModifiers;
                        }
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
            // Logger.Log($"LoadCatalogAsync: Called with forceRefresh={forceRefresh}");
            
            // Return cached data if it's still valid
            if (!forceRefresh && 
                _cachedCatalogItems != null && 
                _cachedCategories != null && 
                (DateTime.Now - _lastCatalogLoadTime) < _cacheExpirationTime)
            {
                // Logger.Log($"LoadCatalogAsync: Returning cached data ({_cachedCatalogItems.Count} items, {_cachedCategories.Count} categories)");
                return (_cachedCatalogItems, _cachedCategories);
            }
            
            // Logger.Log("LoadCatalogAsync: Waiting for catalog lock");
            await _catalogLock.WaitAsync();
            try
            {
                return await LoadCatalogInternalAsync();
            }
            finally
            {
                // Logger.Log("LoadCatalogAsync: Releasing catalog lock");
                _catalogLock.Release();
            }
        }

        private async Task<(List<CatalogEntry> Items, HashSet<string> Categories)> LoadCatalogInternalAsync()
        {
            // Logger.Log($"LoadCatalogInternalAsync: Loading {_loadedFiles.Count} files");
            
            var items = new List<CatalogEntry>();
            _patchFileSkuIds.Clear();
            _skuToFileMapping.Clear();

            // Load all files in the loaded files list, plus their MODIFIED versions
            foreach (var filePath in _loadedFiles)
            {
                if (!File.Exists(filePath))
                {
                    // Logger.Log($"LoadCatalogInternalAsync: Skipping missing file: {filePath}");
                    continue;
                }
                
                // Logger.Log($"LoadCatalogInternalAsync: Loading file: {filePath}");
                var fileJson = await File.ReadAllTextAsync(filePath);
                // Logger.Log($"LoadCatalogInternalAsync: JSON length: {fileJson.Length}");
                
                var fileEntries = JsonSerializer.Deserialize<List<CatalogEntry>>(fileJson, _jsonOptions);
                
                if (fileEntries != null)
                {
                    // Logger.Log($"LoadCatalogInternalAsync: Loaded {fileEntries.Count} items from {Path.GetFileName(filePath)}");
                    
                    // If this is a MODIFIED file, its entries override any base entries already loaded
                    string currentFileName = Path.GetFileNameWithoutExtension(filePath);
                    bool isModifiedFile = currentFileName.EndsWith("MODIFIED", StringComparison.OrdinalIgnoreCase);
                    
                    foreach (var entry in fileEntries)
                    {
                        if (isModifiedFile)
                        {
                            // Remove any base version with the same SKU
                            items.RemoveAll(x => x.SkuId == entry.SkuId);
                        }
                        items.Add(entry);
                        _skuToFileMapping[entry.SkuId] = filePath;
                    }
                }
                else
                {
                    // Logger.Log($"LoadCatalogInternalAsync: Deserialization returned null for {filePath}");
                }

                // Also load the corresponding MODIFIED file if it exists
                // BUT skip if that MODIFIED file is already explicitly in _loadedFiles (it will be processed on its own)
                string baseFileName = Path.GetFileNameWithoutExtension(filePath);
                if (!baseFileName.EndsWith("MODIFIED", StringComparison.OrdinalIgnoreCase))
                {
                    string directory = Path.GetDirectoryName(filePath) ?? "";
                    string extension = Path.GetExtension(filePath);
                    string modifiedFilePath = Path.Combine(directory, $"{baseFileName}MODIFIED{extension}");

                    if (File.Exists(modifiedFilePath) && !_loadedFiles.Contains(modifiedFilePath))
                    {
                        // Logger.Log($"LoadCatalogInternalAsync: Loading modified file: {modifiedFilePath}");
                        var modifiedJson = await File.ReadAllTextAsync(modifiedFilePath);
                        // Logger.Log($"LoadCatalogInternalAsync: Modified JSON length: {modifiedJson.Length}");
                        
                        var modifiedEntries = JsonSerializer.Deserialize<List<CatalogEntry>>(modifiedJson, _jsonOptions);
                        
                        if (modifiedEntries != null)
                        {
                            // Logger.Log($"LoadCatalogInternalAsync: Loaded {modifiedEntries.Count} items from {Path.GetFileName(modifiedFilePath)}");
                            
                            // Modified entries override base entries
                            foreach (var modifiedEntry in modifiedEntries)
                            {
                                // Remove the base version if it exists
                                items.RemoveAll(x => x.SkuId == modifiedEntry.SkuId);
                                // Add the modified version
                                items.Add(modifiedEntry);
                                // Track as coming from the base file (for future saves)
                                _skuToFileMapping[modifiedEntry.SkuId] = filePath;
                            }
                            
                            // Logger.Log($"LoadCatalogInternalAsync: Applied {modifiedEntries.Count} modifications");
                        }
                        else
                        {
                            // Logger.Log($"LoadCatalogInternalAsync: Modified file deserialization returned null");
                        }
                    }
                }
            }

            // Logger.Log($"LoadCatalogInternalAsync: Total items loaded: {items.Count}");
            
            // Get unique categories from all items
            var categories = new HashSet<string>(items.Select(i => i.Type.Name).OrderBy(n => n));
            // Logger.Log($"LoadCatalogInternalAsync: Extracted {categories.Count} categories");
            
            // Update cache
            _cachedCatalogItems = items;
            _cachedCategories = categories;
            _lastCatalogLoadTime = DateTime.Now;
            
            // Logger.Log("LoadCatalogInternalAsync: Returning results");
            return (items, categories);
        }

        public bool IsItemFromPatch(ulong skuId) => _patchFileSkuIds.Contains(skuId);

        public async Task<bool> SaveItemAsync(CatalogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
                        
            await _catalogLock.WaitAsync();
            try
            {
                // Always save to the MODIFIED file based on the catalog path
                await SaveToModifiedFileAsync(entry);
                return true;
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

        private async Task SaveToModifiedFileAsync(CatalogEntry entry)
        {
            // Determine which file this SKU belongs to, or find the appropriate file based on item type if new
            string sourceFile;
            if (_skuToFileMapping.TryGetValue(entry.SkuId, out var mappedFile))
            {
                sourceFile = mappedFile;
                // Logger.Log($"SaveToModifiedFileAsync: SKU {entry.SkuId} maps to {Path.GetFileName(sourceFile)}");
            }
            else if (_loadedFiles.Count > 0)
            {
                // New item - find the appropriate catalog file based on Type.Name
                string itemType = entry.Type?.Name ?? "Boost"; // Default to Boost if no type specified
                
                // First, try to find in loaded files
                sourceFile = _loadedFiles.FirstOrDefault(f => 
                    Path.GetFileNameWithoutExtension(f).Contains(itemType, StringComparison.OrdinalIgnoreCase));
                
                // If not found in loaded files, look for the catalog file in the Data directory
                if (sourceFile == null)
                {
                    var dataDirectory = Path.GetDirectoryName(_loadedFiles[0]) ?? "";
                    var expectedFileName = $"Catalog{itemType}.json";
                    var expectedPath = Path.Combine(dataDirectory, expectedFileName);
                    
                    if (File.Exists(expectedPath))
                    {
                        sourceFile = expectedPath;
                        Debug.WriteLine($"Found catalog file for type '{itemType}' at {expectedPath} (not loaded)");
                    }
                    else
                    {
                        // Fall back to most recently loaded file
                        sourceFile = _loadedFiles[^1];
                        Debug.WriteLine($"Warning: No catalog file found for type '{itemType}', using {Path.GetFileName(sourceFile)}");
                    }
                }
                // Logger.Log($"SaveToModifiedFileAsync: New SKU {entry.SkuId} with type '{itemType}', using file {Path.GetFileName(sourceFile)}");
            }
            else
            {
                throw new InvalidOperationException("No catalog files loaded");
            }

            // Remove existing MODIFIED suffix if present to avoid MODIFIEDMODIFIED
            string baseFileName = Path.GetFileNameWithoutExtension(sourceFile);
            if (baseFileName.EndsWith("MODIFIED", StringComparison.OrdinalIgnoreCase))
            {
                baseFileName = baseFileName.Substring(0, baseFileName.Length - "MODIFIED".Length);
            }

            string directory = Path.GetDirectoryName(sourceFile) ?? "";
            string extension = Path.GetExtension(sourceFile);
            string modifiedFilePath = Path.Combine(directory, $"{baseFileName}MODIFIED{extension}");

            // Logger.Log($"SaveToModifiedFileAsync: Saving to {modifiedFilePath}");

            // Load existing modified file or create new list
            List<CatalogEntry> modifiedItems;
            if (File.Exists(modifiedFilePath))
            {
                var modifiedJson = await File.ReadAllTextAsync(modifiedFilePath);
                modifiedItems = JsonSerializer.Deserialize<List<CatalogEntry>>(modifiedJson, _jsonOptions) ?? new List<CatalogEntry>();
                // Logger.Log($"SaveToModifiedFileAsync: Loaded {modifiedItems.Count} existing items from modified file");
            }
            else
            {
                modifiedItems = new List<CatalogEntry>();
                // Logger.Log("SaveToModifiedFileAsync: Creating new modified file");
            }

            // Remove existing entry with same SKU if it exists
            int beforeCount = modifiedItems.Count;
            modifiedItems.RemoveAll(x => x.SkuId == entry.SkuId);
            if (modifiedItems.Count < beforeCount)
            {
                // Logger.Log($"SaveToModifiedFileAsync: Removed existing entry with SkuId {entry.SkuId}");
            }

            // Add the new/updated entry
            modifiedItems.Add(entry);
            // Logger.Log($"SaveToModifiedFileAsync: Added entry with SkuId {entry.SkuId}, total items: {modifiedItems.Count}");

            // Create backup if file exists
            if (File.Exists(modifiedFilePath))
            {
                string backupPath = modifiedFilePath + ".bak";
                File.Copy(modifiedFilePath, backupPath, true);
            }

            // Save to modified file
            await File.WriteAllTextAsync(modifiedFilePath, JsonSerializer.Serialize(modifiedItems, _jsonOptions));
            // Logger.Log($"SaveToModifiedFileAsync: Successfully saved to {modifiedFilePath}");

            // Invalidate cache to force reload
            _lastCatalogLoadTime = DateTime.MinValue;
        }

        public async Task<bool> DeleteItemAsync(ulong skuId)
        {
            await _catalogLock.WaitAsync();
            try
            {
                // Find which file this SKU belongs to
                if (!_skuToFileMapping.TryGetValue(skuId, out var sourceFile))
                {
                    // Logger.Log($"DeleteItemAsync: SKU {skuId} not found in any loaded file");
                    return false;
                }

                // Logger.Log($"DeleteItemAsync: Deleting SKU {skuId} from {Path.GetFileName(sourceFile)}");

                if (!File.Exists(sourceFile))
                {
                    // Logger.Log($"DeleteItemAsync: Source file not found: {sourceFile}");
                    return false;
                }

                // Create backup
                string backupPath = sourceFile + ".bak";
                File.Copy(sourceFile, backupPath, true);

                try
                {
                    // Load the file
                    var fileJson = await File.ReadAllTextAsync(sourceFile);
                    var fileEntries = JsonSerializer.Deserialize<List<CatalogEntry>>(fileJson, _jsonOptions);

                    if (fileEntries == null)
                    {
                        return false;
                    }

                    // Remove the entry
                    int initialCount = fileEntries.Count;
                    fileEntries.RemoveAll(x => x.SkuId == skuId);

                    if (fileEntries.Count < initialCount)
                    {
                        // Save back to file
                        await File.WriteAllTextAsync(sourceFile, JsonSerializer.Serialize(fileEntries, _jsonOptions));
                        
                        // Remove from mapping
                        _skuToFileMapping.Remove(skuId);
                        _patchFileSkuIds.Remove(skuId);
                        
                        // Invalidate cache
                        _lastCatalogLoadTime = DateTime.MinValue;
                        
                        // Logger.Log($"DeleteItemAsync: Successfully deleted SKU {skuId}");
                        return true;
                    }
                    
                    return false;
                }
                catch
                {
                    // Restore from backup on error
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, sourceFile, true);
                    }
                    throw;
                }
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
            ulong maxSku = 1000;
            
            // Check all currently loaded files
            var (items, _) = await LoadCatalogAsync();
            if (items.Count > 0)
            {
                maxSku = Math.Max(maxSku, items.Max(i => i.SkuId));
            }
            
            // Also scan all JSON files in the Data directory to avoid SKU conflicts
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (Directory.Exists(dataDir))
            {
                // Logger.Log($"GetNextAvailableSkuId: Scanning {dataDir} for all catalog files");
                
                foreach (var file in Directory.GetFiles(dataDir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(json, _jsonOptions);
                        
                        if (entries?.Count > 0)
                        {
                            var fileMax = entries.Max(i => i.SkuId);
                            maxSku = Math.Max(maxSku, fileMax);
                            // Logger.Log($"GetNextAvailableSkuId: {Path.GetFileName(file)} max SKU: {fileMax}");
                        }
                    }
                    catch
                    {
                        // Skip files that can't be deserialized (not catalog files)
                        // Logger.Log($"GetNextAvailableSkuId: Skipping {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            
            // Logger.Log($"GetNextAvailableSkuId: Returning {maxSku + 1}");
            return maxSku + 1;
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
            if (_prototypeNameCache.TryGetValue(prototypeId, out string? name))
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
                // Get the full item from catalog, update its type modifiers, then save to MODIFIED file
                var fullItem = await GetItemBySkuIdAsync(item.SkuId);
                if (fullItem == null)
                    return false;
                
                // Update the type modifiers
                fullItem.TypeModifiers = item.TypeModifiers;
                
                // Save to MODIFIED file
                await SaveToModifiedFileAsync(fullItem);
                
                _lastCatalogLoadTime = DateTime.MinValue;
                return true;
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        public async Task<CatalogEntry?> GetItemBySkuIdAsync(ulong skuId)
        {
            try
            {
                // Make sure the catalog is loaded
                await LoadCatalogAsync();
        
                // Find the item with the matching SkuId
                return _cachedCatalogItems?.FirstOrDefault(item => item.SkuId == skuId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting item by SkuId: {ex.Message}");
                return null;
            }
        }

        public async Task<CatalogEntry?> GetItemByPrototypeIdAsync(ulong prototypeId)
        {
            try
            {
                // Make sure the catalog is loaded
                await LoadCatalogAsync();
        
                // Find the item with the matching prototype ID in GuidItems
                return _cachedCatalogItems?.FirstOrDefault(item => 
                    item.GuidItems.Any(g => g.ItemPrototypeRuntimeIdForClient == prototypeId));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting item by prototype ID: {ex.Message}");
                return null;
            }
        }
    }
}        
