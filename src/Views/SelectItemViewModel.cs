using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using CatalogManager.Services;
using System.IO;

namespace CatalogManager.ViewModels
{
    public class SelectItemViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly object _itemsLock = new object();
        private CancellationTokenSource _filterCts;
        private CancellationTokenSource _loadCts;
        private readonly Dictionary<string, string> _displayNameMapping;

        
        // Use CollectionViewSource for better filtering performance
        private readonly CollectionViewSource _itemsViewSource = new CollectionViewSource();
        
        private ObservableCollection<Category> _categories;
        private Category _selectedCategory;
        private string _searchFilter = "";
        private ObservableCollection<ItemDisplay> _items;
        private bool _isLoading;
        private string _statusMessage;
        private ItemDisplay _selectedItem;
        private ObservableCollection<ItemDisplay> _selectedItems = new();
        private bool _hideExistingItems;

        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    LoadItemsForCategoryAsync().ConfigureAwait(false);
                }
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                {
                    // Debounce search input
                    _filterCts?.Cancel();
                    _filterCts = new CancellationTokenSource();
                    var token = _filterCts.Token;
                    
                    Task.Delay(300, token).ContinueWith(t => 
                    {
                        if (!t.IsCanceled)
                        {
                            Application.Current.Dispatcher.Invoke(() => FilterItems());
                        }
                    }, token);
                }
            }
        }

        public ICollectionView FilteredItems => _itemsViewSource.View;

        public ItemDisplay SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public ObservableCollection<ItemDisplay> SelectedItems
        {
            get => _selectedItems;
            set => SetProperty(ref _selectedItems, value);
        }

        public bool HasSelectedItems => _selectedItems?.Count > 0;
        
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool HideExistingItems
        {
            get => _hideExistingItems;
            set
            {
                if (SetProperty(ref _hideExistingItems, value))
                {
                    FilterItems();
                }
            }
        }

        public SelectItemViewModel(CatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _items = new ObservableCollection<ItemDisplay>();
            _itemsViewSource.Source = _items;

            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "display_names.json");
            var jsonContent = File.ReadAllText(jsonPath);
            _displayNameMapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
            
            // Initialize categories asynchronously
            Task.Run(InitializeCategoriesAsync);
        }

        private async Task InitializeCategoriesAsync()
        {
            try
            {
                ObservableCollection<Category> categories;
                
                // Try to load categories from config file
                var categoriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "categories.json");
                
                if (File.Exists(categoriesPath))
                {
                    var categoriesJson = await File.ReadAllTextAsync(categoriesPath);
                    var categoriesList = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(categoriesJson);
                    categories = new ObservableCollection<Category>(categoriesList ?? new List<Category>());
                }
                else
                {
                    // Fallback to default categories if file doesn't exist
                    categories = new ObservableCollection<Category>
                    {
                        new Category { Path = "Entity/Items/Consumables", DisplayName = "Consumables", IsInventoryType = false },
                        new Category { Path = "Entity/Items/CharacterTokens", DisplayName = "Character Tokens", IsInventoryType = false },
                        new Category { Path = "Entity/Items/Costumes", DisplayName = "Costumes", IsInventoryType = false },
                        new Category { Path = "Entity/Items/CurrencyItems", DisplayName = "Currency Items", IsInventoryType = false },
                        new Category { Path = "Entity/Items/Pets", DisplayName = "Pets", IsInventoryType = false },
                        new Category { Path = "Entity/Items/Crafting", DisplayName = "Crafting", IsInventoryType = false },
                        new Category { Path = "Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear", DisplayName = "Stash Tabs", IsInventoryType = true },
                        new Category { 
                            Path = "Entity/Items/Test|Entity/Items/Artifacts/Prototypes/Tier1Artifacts/RaidTest|Entity/Items/Medals/MedalBlueprints/Endgame/TestMedals", 
                            DisplayName = "Test Gear", 
                            IsInventoryType = false 
                        },
                    };
                    
                    // Create the default config file for future edits
                    var defaultJson = System.Text.Json.JsonSerializer.Serialize(categories, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    Directory.CreateDirectory(Path.GetDirectoryName(categoriesPath)!);
                    await File.WriteAllTextAsync(categoriesPath, defaultJson);
                }

                await Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    Categories = categories;
                    SelectedCategory = Categories.FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    StatusMessage = $"Error initializing categories: {ex.Message}";
                });
            }
        }

        private async Task LoadItemsForCategoryAsync()
        {
            if (SelectedCategory == null) return;

            try
            {
                // Cancel any previous load operation
                _loadCts?.Cancel();
                _loadCts = new CancellationTokenSource();
                var token = _loadCts.Token;
                
                IsLoading = true;
                StatusMessage = "Loading items...";
                
                // Get existing catalog items to exclude
                var catalogItems = await _catalogService.GetItemsAsync("All", "", token);
                var existingProtoIds = catalogItems.Select(c => c.GuidItems[0].ItemPrototypeRuntimeIdForClient).ToHashSet();
                
                await Task.Run(() => 
                {
                    var validPaths = new[]
                    {
                        "Entity/Items/Consumables",
                        "Entity/Items/CharacterTokens",
                        "Entity/Items/Costumes",
                        "Entity/Items/CurrencyItems",
                        "Entity/Items/Crafting",
                        "Entity/Items/Test",
                        "Entity/Items/Artifacts/Prototypes/Tier1Artifacts/RaidTest",
                        "Entity/Items/Medals/MedalBlueprints/Endgame/TestMedals",
                        "Entity/Items/Pets"
                    };

                    // Process items in batches for better responsiveness
                    var newItems = new List<ItemDisplay>();
                    
                    // Process avatar gear items
                    if (SelectedCategory.Path.Contains("AvatarGear"))
                    {
                        var avatarGearItems = GameDatabase.DataDirectory
                            .IteratePrototypesInHierarchy<PlayerStashInventoryPrototype>(PrototypeIterateFlags.None)
                            .Where(protoId => GameDatabase.GetPrototypeName(protoId).StartsWith("Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear"))
                            .Select(protoId => new ItemDisplay
                            {
                                Id = protoId,
                                FullPath = GameDatabase.GetPrototypeName(protoId),
                                ExistsInCatalog = existingProtoIds.Contains((ulong)protoId),
                                DisplayNameMapping = _displayNameMapping

                            })
                            .ToList();
                            
                        newItems.AddRange(avatarGearItems);
                    }

                    
                    // Process costume items
                    if (SelectedCategory.Path.Contains("Costumes"))
                    {
                        var costumeItems = GameDatabase.DataDirectory
                            .IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.None)
                            .Select(protoId => (id: protoId, proto: GameDatabase.GetPrototype<ItemPrototype>(protoId)))
                            .Where(item => GameDatabase.GetPrototypeName(item.id).StartsWith("Entity/Items/Costumes"))
                            .Where(item => 
                                item.proto.DesignState == DesignWorkflowState.Live ||
                                item.proto.DesignState == DesignWorkflowState.DevelopmentOnly)
                            .Select(item => new ItemDisplay
                            {
                                Id = item.id,
                                FullPath = GameDatabase.GetPrototypeName(item.id),
                                ExistsInCatalog = existingProtoIds.Contains((ulong)item.id),
                                DisplayNameMapping = _displayNameMapping

                            })
                            .ToList();
                            
                        newItems.AddRange(costumeItems);
                    }

                    
                    // Process regular items
                    var regularItems = GameDatabase.DataDirectory
                        .IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.None)
                        .Select(protoId => (id: protoId, proto: GameDatabase.GetPrototype<ItemPrototype>(protoId)))
                        .Where(item => validPaths.Except(new[] { "Entity/Items/Costumes" }).Any(path => 
                            GameDatabase.GetPrototypeName(item.id).StartsWith(path)))
                        .Where(item => 
                            item.proto.DesignState == DesignWorkflowState.Live ||
                            item.proto.DesignState == DesignWorkflowState.DevelopmentOnly)
                        .Select(item => new ItemDisplay
                        {
                            Id = item.id,
                            FullPath = GameDatabase.GetPrototypeName(item.id),
                            ExistsInCatalog = existingProtoIds.Contains((ulong)item.id),
                            DisplayNameMapping = _displayNameMapping

                        })
                        .ToList();
                        
                    newItems.AddRange(regularItems);
                    
                    // Filter by selected category
                    var categoryItems = newItems.Where(item => 
                    {
                        if (SelectedCategory.Path.Contains("|"))
                        {
                            // Split the compound path and check if item starts with any of the paths
                            var paths = SelectedCategory.Path.Split('|');
                            return paths.Any(path => item.FullPath.StartsWith(path));
                        }
                        return item.FullPath.StartsWith(SelectedCategory.Path);
                    }).ToList();
                    
                    // Update UI on main thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        lock (_itemsLock)
                        {
                            _items.Clear();
                            foreach (var item in categoryItems)
                            {
                                _items.Add(item);
                            }
                        }

                        StatusMessage = $"Loaded {_items.Count} items";
                        FilterItems();
                        
                    });
                    
                }, token);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Loading cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading items: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void FilterItems()
        {
            try
            {
                if (_itemsViewSource.View == null)
                    return;
                    
                if (string.IsNullOrWhiteSpace(SearchFilter) && !HideExistingItems)
                {
                    _itemsViewSource.View.Filter = null;
                }
                else
                {
                    var searchTerms = SearchFilter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    _itemsViewSource.View.Filter = obj => 
                    {
                        if (obj is ItemDisplay item)
                        {
                            // Skip items that exist in catalog if hide filter is on
                            if (HideExistingItems && item.ExistsInCatalog)
                                return false;

                            // Search in both display name and path
                            var displayName = _displayNameMapping.TryGetValue(item.FullPath, out string name) ? name : "";
                            return !searchTerms.Any() || searchTerms.All(term => 
                                item.FullPath.Contains(term, StringComparison.OrdinalIgnoreCase) || 
                                displayName.Contains(term, StringComparison.OrdinalIgnoreCase));
                        }
                        return false;
                    };
                }
                
                StatusMessage = $"Found {_itemsViewSource.View.Cast<object>().Count()} items";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error filtering items: {ex.Message}";
            }
        }
        
        // Helper method for property change notification
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
                
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Category
    {
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public bool IsInventoryType { get; set; }
    }

    public class ItemDisplay
    {
        public Dictionary<string, string> DisplayNameMapping { get; set; }
        public PrototypeId Id { get; set; }
        public string FullPath { get; set; }
        public bool ExistsInCatalog { get; set; }
        public string DisplayName 
        { 
            get
            {
                if (DisplayNameMapping?.TryGetValue(FullPath, out string displayName) == true && displayName != "N/A")
                {
                    return $"{displayName} ({FullPath})";
                }
                return FullPath;
            }
        }
    }
}
