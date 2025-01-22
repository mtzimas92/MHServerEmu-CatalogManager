using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MHServerEmu.Games.GameData;
using System.Linq;
using System.Collections.Generic;
using MHServerEmu.Games.GameData.Prototypes;
using CatalogManager.Services;
using System.Diagnostics;
using System.IO;  // For File operations


namespace CatalogManager.ViewModels
{
    public class SelectItemViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private ObservableCollection<Category> _categories;
        private Category _selectedCategory;
        private string _searchFilter = "";
        private ObservableCollection<ItemDisplay> _items;
        private ObservableCollection<ItemDisplay> _filteredItems;
        private ItemDisplay _selectedItem;

        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged();
            }
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                LoadItemsForCategory();
                OnPropertyChanged();
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                _searchFilter = value;
                FilterItems();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ItemDisplay> FilteredItems
        {
            get => _filteredItems;
            set
            {
                _filteredItems = value;
                OnPropertyChanged();
            }
        }

        public ItemDisplay SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public SelectItemViewModel(CatalogService catalogService)
        {
            _catalogService = catalogService;
            InitializeCategories();
            //ExportCharacterPrototypesToJson().Wait();
        }

        private void InitializeCategories()
        {
            Categories = new ObservableCollection<Category>
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

            // Select first category by default
            SelectedCategory = Categories.FirstOrDefault();
        }

        private async void LoadItemsForCategory()
        {
            if (SelectedCategory == null) return;

            var catalogItems = await _catalogService.GetItemsAsync("All", "");
            var existingProtoIds = catalogItems.Select(c => c.GuidItems[0].ItemPrototypeRuntimeIdForClient).ToHashSet();

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

            var avatarGearItems = GameDatabase.DataDirectory
                .IteratePrototypesInHierarchy<PlayerStashInventoryPrototype>(PrototypeIterateFlags.None)
                .Where(protoId => GameDatabase.GetPrototypeName(protoId).StartsWith("Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear"))
                .Where(protoId => !existingProtoIds.Contains((ulong)protoId))
                .Select(protoId => new ItemDisplay
                {
                    Id = protoId,
                    FullPath = GameDatabase.GetPrototypeName(protoId)
                })
                .ToList();

            var costumeItems = GameDatabase.DataDirectory
                .IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.None)
                .Select(protoId => (id: protoId, proto: GameDatabase.GetPrototype<ItemPrototype>(protoId)))
                .Where(item => GameDatabase.GetPrototypeName(item.id).StartsWith("Entity/Items/Costumes"))
                .Where(item => 
                    item.proto.DesignState == DesignWorkflowState.Live ||
                    item.proto.DesignState == DesignWorkflowState.DevelopmentOnly)
                .Where(item => !existingProtoIds.Contains((ulong)item.id))
                .Select(item => new ItemDisplay
                {
                    Id = item.id,
                    FullPath = GameDatabase.GetPrototypeName(item.id)
                })
                .ToList();

            var regularItems = GameDatabase.DataDirectory
                .IteratePrototypesInHierarchy<ItemPrototype>(PrototypeIterateFlags.None)
                .Select(protoId => (id: protoId, proto: GameDatabase.GetPrototype<ItemPrototype>(protoId)))
                .Where(item => validPaths.Except(new[] { "Entity/Items/Costumes" }).Any(path => GameDatabase.GetPrototypeName(item.id).StartsWith(path)))
                .Where(item => 
                    item.proto.DesignState == DesignWorkflowState.Live ||
                    item.proto.DesignState == DesignWorkflowState.DevelopmentOnly)
                .Where(item => !existingProtoIds.Contains((ulong)item.id))
                .Select(item => new ItemDisplay
                {
                    Id = item.id,
                    FullPath = GameDatabase.GetPrototypeName(item.id)
                })
                .ToList();

            var allItems = regularItems.Concat(avatarGearItems).Concat(costumeItems).ToList();
            
            // Filter by selected category
            var categoryItems = allItems.Where(item => {
                if (SelectedCategory.Path.Contains("|"))
                {
                    // Split the compound path and check if item starts with any of the paths
                    var paths = SelectedCategory.Path.Split('|');
                    return paths.Any(path => item.FullPath.StartsWith(path));
                }
                return item.FullPath.StartsWith(SelectedCategory.Path);
            }).ToList();            
            _items = new ObservableCollection<ItemDisplay>(categoryItems);
            FilterItems();
        }
        private void FilterItems()
        {
              if (string.IsNullOrWhiteSpace(SearchFilter))
                  FilteredItems = _items;
              else
              {
                  var filtered = _items.Where(item => 
                      item.FullPath.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                  FilteredItems = new ObservableCollection<ItemDisplay>(filtered);
              }
        }
        // This is a piece of code that can be used to get various information from specific areas. It's currentl
        // not used but it's setup to find LiveTuning enabled/disabled properties (for events). The method name is 
        // left over from previous iterations. This can be called in line 82, on startup of add new items. Leaving it here. 
        // public async Task ExportCharacterPrototypesToJson()
        // {
        //     var allPrototypes = GameDatabase.DataDirectory
        //         .IteratePrototypesInHierarchy<Prototype>(PrototypeIterateFlags.None)
        //         .Select(protoId => {
        //             var proto = GameDatabase.GetPrototype<Prototype>(protoId);
        //             var liveTuningProperty = proto.GetType().GetProperty("LiveTuningDefaultEnabled");
        //             return new
        //             {
        //                 Path = GameDatabase.GetPrototypeName(protoId),
        //                 Type = proto.GetType().Name,
        //                 HasLiveTuning = liveTuningProperty != null,
        //                 LiveTuningValue = liveTuningProperty?.GetValue(proto)
        //             };
        //         })
        //         .Where(x => x.HasLiveTuning && x.LiveTuningValue != null && !(bool)x.LiveTuningValue)
        //         .OrderBy(x => x.Path)
        //         .ToList();

        //     var output = allPrototypes.Select(p => $"Path: {p.Path}\nType: {p.Type}\nLiveTuningDefaultEnabled: {p.LiveTuningValue}\n\n");
        //     await File.WriteAllLinesAsync("live_tuning_disabled.txt", output);
        // }

                        
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
          public PrototypeId Id { get; set; }
          public string FullPath { get; set; }
          public string DisplayName => FullPath;  // Changed to show full path
    }
    
}
