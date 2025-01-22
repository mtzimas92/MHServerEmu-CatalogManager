using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CatalogManager.Services;
using CatalogManager.Commands;
using System.Windows;
using CatalogManager.Views;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;


namespace CatalogManager.ViewModels
{
    public class AddItemViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly CatalogEntry _existingItem;
        public ICommand OpenSelectItemCommand { get; private set; }
        private int _existingTypeOrder;
        private List<TypeModifier> _existingTypeModifiers;
        private ObservableCollection<string> _availableTypeModifiers;
        private ObservableCollection<string> _selectedTypeModifiers = new();
        public string WindowTitle => _existingItem == null ? "Add New Item" : "Edit Item";
        public bool IsNewItem => _existingItem == null;

        private ulong _skuId;
        public ulong SkuId
        {
            get => _skuId;
            set
            {
                _skuId = value;
                OnPropertyChanged();
            }
        }

        private ulong _prototypeId;
        public ulong PrototypeId
        {
            get => _prototypeId;
            set
            {
                _prototypeId = value;
                OnPropertyChanged();
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        private int _price;
        public int Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableTypeModifiers
        {
            get => _availableTypeModifiers;
            set
            {
                _availableTypeModifiers = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> SelectedTypeModifiers 
        { 
            get => _selectedTypeModifiers;
            set
            {
                _selectedTypeModifiers = value;
                OnPropertyChanged();
            }
        }
        public void UpdateSelectedModifiers(ListBox listBox)
        {
            SelectedTypeModifiers.Clear();
            foreach (var item in listBox.SelectedItems)
            {
                SelectedTypeModifiers.Add(item.ToString());
            }
        }
        private GameDatabaseItem _selectedDatabaseItem;
        public GameDatabaseItem SelectedDatabaseItem
        {
            get => _selectedDatabaseItem;
            set
            {
                _selectedDatabaseItem = value;
                if (value != null)
                {
                    PrototypeId = (ulong)value.Id;
                    Title = value.Name;
                    Description = "From Game Database";
                }
                OnPropertyChanged();
            }
        }

        private void UpdateAvailableModifiers()
        {
            var categoryModifiers = _catalogService.GetCategoryModifiers(SelectedType);
            //Debug.WriteLine($"Available modifiers: {string.Join(", ", categoryModifiers)}");

            AvailableTypeModifiers = new ObservableCollection<string>(categoryModifiers);
            //SelectedTypeModifiers = new ObservableCollection<string>();
            //Debug.WriteLine($"Selected modifiers: {string.Join(", ", SelectedTypeModifiers)}");

        }

        public ObservableCollection<string> ItemTypes { get; } = new(new[]
        {
            "Boost", "Bundle", "Chest", "Costume", "Hero", "Service", "TeamUp"
        });

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set
            {
                _selectedType = value;
                UpdateAvailableModifiers();
                OnPropertyChanged();
            }
        }

        private List<TypeModifier> GetTypeModifiersForCategory(string category)
        {
            Debug.WriteLine($"Selected modifiers count: {SelectedTypeModifiers.Count}");
            var modifiers = SelectedTypeModifiers.Select(name => new TypeModifier 
            { 
                Name = name, 
                Order = 2 
            }).ToList();
            Debug.WriteLine($"Created TypeModifiers count: {modifiers.Count}");
            return modifiers;
        }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddItemViewModel(CatalogService catalogService, CatalogEntry existingItem = null)
        {
            _catalogService = catalogService;
            _existingItem = existingItem;
            //LoadGameDatabaseItems();

            if (existingItem != null)
                LoadExistingItem(existingItem);
            else
                InitializeNewItem();
            OpenSelectItemCommand = new RelayCommand(OpenSelectItemWindow);
            SaveCommand = new RelayCommand(Save, CanSave);
            CancelCommand = new RelayCommand(Cancel);
        }
        private async void InitializeNewItem()
        {
            SkuId = await _catalogService.GetNextAvailableSkuId();
            SelectedType = ItemTypes.First();
        }
        private void LoadExistingItem(CatalogEntry item)
        {
            SkuId = item.SkuId;
            PrototypeId = item.GuidItems[0].ItemPrototypeRuntimeIdForClient;
            Title = item.LocalizedEntries[0].Title;
            Description = item.LocalizedEntries[0].Description;
            Price = item.LocalizedEntries[0].ItemPrice;
            SelectedType = item.Type.Name;
            
            // Update available modifiers first
            UpdateAvailableModifiers();
            
            // Set the selected modifiers from the existing item
            SelectedTypeModifiers = new ObservableCollection<string>(item.TypeModifiers.Select(m => m.Name));
            _existingTypeOrder = item.Type.Order;
            _existingTypeModifiers = item.TypeModifiers;
            //Debug.WriteLine($"Loading modifiers: {string.Join(", ", item.TypeModifiers.Select(m => m.Name))}");

            // Update the ListBox selections to match existing modifiers
            UpdateListBoxSelections();
        }
        public void UpdateListBoxSelections()
        {
            var listBox = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?.FindName("TypeModifiersListBox") as ListBox;

            if (listBox != null)
            {
                var selectedModifiers = SelectedTypeModifiers.ToList(); // Create a fixed copy
                listBox.ItemContainerGenerator.StatusChanged += (s, e) =>
                {
                    if (listBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    {
                        for (int i = 0; i < listBox.Items.Count; i++)
                        {
                            var item = listBox.Items[i];
                            var listBoxItem = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                            if (listBoxItem != null)
                            {
                                listBoxItem.IsSelected = selectedModifiers.Contains(item.ToString());
                            }
                        }
                    }
                };
            }
        }

        private async void Save()
        {
            Debug.WriteLine($"Saving item with Type: {SelectedType}");
            Debug.WriteLine($"Selected modifiers: {string.Join(", ", SelectedTypeModifiers)}");
            Debug.WriteLine($"TypeModifiers being saved: {string.Join(", ", GetTypeModifiersForCategory(SelectedType).Select(m => m.Name))}");
            var entry = new CatalogEntry
            {
                SkuId = SkuId,
                GuidItems = new List<GuidItem>
                {
                    new()
                    {
                        PrototypeGuid = 0,
                        ItemPrototypeRuntimeIdForClient = PrototypeId,
                        Quantity = 1
                    }
                },
                LocalizedEntries = new List<LocalizedEntry>
                {
                    new()
                    {
                        LanguageId = "en_us",
                        Title = Title,
                        Description = Description,
                        ReleaseDate = "",
                        ItemPrice = Price
                    }
                },
                Type = new ItemType
                {
                    Name = SelectedType,
                    Order = _existingItem?.Type.Order ?? 999
                },
                TypeModifiers = GetTypeModifiersForCategory(SelectedType)
            };

            await _catalogService.SaveItemAsync(entry);
            CloseWindow(true);
        }

        private void OpenSelectItemWindow()
        {
            var selectWindow = new SelectItemWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = new SelectItemViewModel(_catalogService)
            };

            if (selectWindow.ShowDialog() == true && selectWindow.DataContext is SelectItemViewModel vm && vm.SelectedItem != null)
            {
                PrototypeId = (ulong)vm.SelectedItem.Id;
                Title = vm.SelectedItem.DisplayName;
                SetSmartDefaults(vm.SelectedItem);
            }
        }
        private void SetSmartDefaults(ItemDisplay selectedItem)
        {
            string path = selectedItem.FullPath;

            // Stash and Service items
            if (path.Contains("/StashInventories/PageProtos/"))
            {
                SelectedType = "Service";
                Description = "Stash Tab";
            }
            // Costumes
            else if (path.Contains("/Costumes/Prototypes/"))
            {
                SelectedType = "Costume";
                Description = "Character Costume";
            }
            // Character Tokens and Heroes
            else if (path.Contains("/CharacterTokens/Prototypes/"))
            {
                if (path.Contains("/TeamUps/"))
                {
                    SelectedType = "Boost";
                    Description = "Team-Up Token";
                }
                else
                {
                    SelectedType = "Hero";
                    Description = "Character Token";
                }
            }
            // Consumables with various types
            else if (path.Contains("/Consumables/Prototypes/"))
            {
                if (path.Contains("/FortuneCard/"))
                {
                    if (path.Contains("/MysteryBox/"))
                    {
                        SelectedType = path.Contains("Bundle") ? "Bundle" : "Boost";
                    }
                    else
                    {
                        SelectedType = "Chest";
                    }
                    Description = "Fortune Card Item";
                }
                else if (path.Contains("/DailyGift/"))
                {
                    SelectedType = path.Contains("Bundle") ? "Bundle" : "Boost";
                    Description = "Daily Gift Item";
                }
                else
                {
                    SelectedType = "Boost";
                    Description = "Consumable Item";
                }
            }
            // Crafting items
            else if (path.Contains("/Crafting/"))
            {
                SelectedType = "Boost";
                Description = "Crafting Material";
            }
            // Currency items
            else if (path.Contains("/CurrencyItems/"))
            {
                SelectedType = path.Contains("Bundle") ? "Bundle" : "Boost";
                Description = "Currency Item";
            }
            // Pets
            else if (path.Contains("/Pets/"))
            {
                SelectedType = "Boost";
                Description = "Pet Item";
            }
        }
        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrWhiteSpace(Description) &&
                   PrototypeId > 0;
        }

        private void Cancel()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool result)
        {
            if (System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) is Window window)
            {
                window.DialogResult = result;
                window.Close();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class GameDatabaseItem
    {
        public PrototypeId Id { get; set; }
        public string Name { get; set; }
    }
}
