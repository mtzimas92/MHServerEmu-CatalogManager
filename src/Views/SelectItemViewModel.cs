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
using CatalogManager.Models;
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
        
        private ObservableCollection<LocalizedCategory> _categories;
        private LocalizedCategory _selectedCategory;
        private string _searchFilter = "";
        private ObservableCollection<ItemDisplay> _items;
        private bool _isLoading;
        private string _statusMessage;
        private ItemDisplay _selectedItem;
        private ObservableCollection<ItemDisplay> _selectedItems = new();
        private bool _hideExistingItems;

        public ObservableCollection<LocalizedCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public LocalizedCategory SelectedCategory
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

        private bool _hidePrototypePaths;
        public bool HidePrototypePaths
        {
            get => _hidePrototypePaths;
            set
            {
                if (SetProperty(ref _hidePrototypePaths, value))
                {
                    // Refresh all items to update their display names
                    RefreshItemDisplayNames();
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
                ObservableCollection<LocalizedCategory> categories;
                
                // Use LocalizedCategory objects instead of simple Category
                categories = new ObservableCollection<LocalizedCategory>
                {
                    new LocalizedCategory("Entity/Items/Consumables", "Consumables", false),
                    new LocalizedCategory("Entity/Items/CharacterTokens", "CharacterTokens", false),
                    new LocalizedCategory("Entity/Items/Costumes", "Costumes", false),
                    new LocalizedCategory("Entity/Items/CurrencyItems", "CurrencyItems", false),
                    new LocalizedCategory("Entity/Items/Pets", "Pets", false),
                    new LocalizedCategory("Entity/Items/Crafting", "Crafting", false),
                    new LocalizedCategory("Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear", "StashTabs", true),
                    new LocalizedCategory("Entity/Items/Test|Entity/Items/Artifacts/Prototypes/Tier1Artifacts/RaidTest|Entity/Items/Medals/MedalBlueprints/Endgame/TestMedals", "TestGear", false),
                };

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
                    StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.ErrorInitializing", ex.Message);
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
                StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadingItems");
                
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
                                item.ViewModel = this;
                                _items.Add(item);
                            }
                        }

                        StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadedItems", _items.Count);
                        FilterItems();
                        
                    });
                    
                }, token);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadingCancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.ErrorLoading", ex.Message);
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
                
                StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.FoundItems", _itemsViewSource.View.Cast<object>().Count());
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("SelectItemWindow.Status.ErrorFiltering", ex.Message);
            }
        }

        private void RefreshItemDisplayNames()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Trigger a refresh of the view to update all display names
                _itemsViewSource.View?.Refresh();
            });
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
        public SelectItemViewModel ViewModel { get; set; }
        
        public string DisplayName 
        { 
            get
            {
                if (DisplayNameMapping?.TryGetValue(FullPath, out string displayName) == true && displayName != "N/A")
                {
                    // Check if we should hide the prototype path
                    if (ViewModel?.HidePrototypePaths == true)
                    {
                        return displayName;
                    }
                    return $"{displayName} ({FullPath})";
                }
                return FullPath;
            }
        }
    }
}
