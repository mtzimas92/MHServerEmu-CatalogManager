using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatalogManager.Commands;
using CatalogManager.Services;
using CatalogManager.Views;
using MHServerEmu.Games.GameData;
using System.Windows.Controls.Primitives;


namespace CatalogManager.ViewModels
{
    public class AddItemViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly CatalogEntry _existingItem;
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _saveCts;
        
        // Command properties
        public ICommand OpenSelectItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        
        // State tracking
        private int _existingTypeOrder;
        private List<TypeModifier> _existingTypeModifiers;
        private bool _isClosing;
        private bool _isSaving;
        private string _statusMessage;
        
        // UI properties
        public string WindowTitle => _existingItem == null ? "Add New Item" : "Edit Item";
        public bool IsNewItem => _existingItem == null;
        
        private ulong _skuId;
        public ulong SkuId
        {
            get => _skuId;
            set => SetProperty(ref _skuId, value);
        }

        private ulong _prototypeId;
        public ulong PrototypeId
        {
            get => _prototypeId;
            set => SetProperty(ref _prototypeId, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    // Refresh save command can-execute state
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private int _price;
        public int Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }
        
        private ObservableCollection<string> _availableTypeModifiers;
        public ObservableCollection<string> AvailableTypeModifiers
        {
            get => _availableTypeModifiers;
            set => SetProperty(ref _availableTypeModifiers, value);
        }

        private ObservableCollection<string> _selectedTypeModifiers = new();
        public ObservableCollection<string> SelectedTypeModifiers 
        { 
            get => _selectedTypeModifiers;
            set => SetProperty(ref _selectedTypeModifiers, value);
        }
        
        private GameDatabaseItem _selectedDatabaseItem;
        public GameDatabaseItem SelectedDatabaseItem
        {
            get => _selectedDatabaseItem;
            set
            {
                if (SetProperty(ref _selectedDatabaseItem, value) && value != null)
                {
                    PrototypeId = (ulong)value.Id;
                    Title = value.Name;
                    Description = "From Game Database";
                }
            }
        }
        
        public bool IsSaving
        {
            get => _isSaving;
            private set => SetProperty(ref _isSaving, value);
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private ObservableCollection<string> _itemTypes;
        public ObservableCollection<string> ItemTypes 
        { 
            get => _itemTypes;
            private set => SetProperty(ref _itemTypes, value);
        }

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                {
                    UpdateAvailableModifiers();
                }
            }
        }

        private int _quantity = 1; // Default to 1
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, Math.Max(1, value)); // Ensure minimum of 1
        }

        public AddItemViewModel(CatalogService catalogService, CatalogEntry existingItem = null)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _existingItem = existingItem;
            
            // Initialize commands
            OpenSelectItemCommand = new AsyncRelayCommand(OpenSelectItemWindowAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(Cancel);
            
            // Initialize item types
            _itemTypes = new ObservableCollection<string>(new[]
            {
                "Boost", "Bundle", "Chest", "Costume", "Hero", "Service", "TeamUp"
            });
            
            // Initialize the view model
            if (existingItem != null)
                LoadExistingItem(existingItem);
            else
                InitializeNewItemAsync().ConfigureAwait(false);
        }
        
        private async Task InitializeNewItemAsync()
        {
            try
            {
                StatusMessage = "Initializing...";
                SkuId = await _catalogService.GetNextAvailableSkuId();
                SelectedType = ItemTypes.FirstOrDefault();
                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error initializing: {ex.Message}";
                Debug.WriteLine($"Error initializing new item: {ex}");
            }
        }
        
        private void LoadExistingItem(CatalogEntry item)
        {
            try
            {
                StatusMessage = "Loading item...";
                
                // Load basic properties
                SkuId = item.SkuId;
                PrototypeId = item.GuidItems[0].ItemPrototypeRuntimeIdForClient;
                Title = item.LocalizedEntries[0].Title;
                Description = item.LocalizedEntries[0].Description;
                Price = item.LocalizedEntries[0].ItemPrice;
                Quantity = item.GuidItems[0].Quantity; // Add this line to load quantity

                SelectedType = item.Type.Name;
                
                // Store existing type information
                _existingTypeOrder = item.Type.Order;
                _existingTypeModifiers = item.TypeModifiers;
                
                // Update available modifiers first
                UpdateAvailableModifiers();
                
                // Set the selected modifiers from the existing item
                SelectedTypeModifiers = new ObservableCollection<string>(
                    item.TypeModifiers.Select(m => m.Name));
                
                // Update the ListBox selections to match existing modifiers
                UpdateListBoxSelections();
                
                StatusMessage = "Item loaded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading item: {ex.Message}";
                Debug.WriteLine($"Error loading existing item: {ex}");
            }
        }
        
        public void UpdateListBoxSelections()
        {
            var listBox = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?.FindName("TypeModifiersListBox") as ListBox;

            if (listBox != null)
            {
                var selectedModifiers = SelectedTypeModifiers.ToList(); // Create a fixed copy
                
                // Handle the case when items aren't generated yet
                if (listBox.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                {
                    listBox.ItemContainerGenerator.StatusChanged += (s, e) => 
                        UpdateListBoxItemsSelection(listBox, selectedModifiers);
                }
                else
                {
                    UpdateListBoxItemsSelection(listBox, selectedModifiers);
                }
            }
        }
        
        private void UpdateListBoxItemsSelection(ListBox listBox, List<string> selectedModifiers)
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
        }
        
        public void UpdateSelectedModifiers(ListBox listBox)
        {
            if (listBox == null) return;
            
            SelectedTypeModifiers.Clear();
            foreach (var item in listBox.SelectedItems)
            {
                SelectedTypeModifiers.Add(item.ToString());
            }
        }
        
        private void UpdateAvailableModifiers()
        {
            try
            {
                var categoryModifiers = _catalogService.GetCategoryModifiers(SelectedType);
                AvailableTypeModifiers = new ObservableCollection<string>(categoryModifiers);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating modifiers: {ex.Message}");
                AvailableTypeModifiers = new ObservableCollection<string>();
            }
        }
        
        private List<TypeModifier> GetTypeModifiersForCategory(string category)
        {
            var typeOrder = _existingItem?.Type.Order ?? 999;
            return SelectedTypeModifiers
                .Select(name => new TypeModifier { Name = name, Order = typeOrder })
                .ToList();
        }
        
        private async Task OpenSelectItemWindowAsync()
        {
            try
            {
                StatusMessage = "Opening item selector...";
                
                // Create the view model first
                var selectViewModel = new SelectItemViewModel(_catalogService);
                
                // Create the window with the view model
                var selectWindow = new SelectItemWindow
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = selectViewModel
                };

                // Show the window as a dialog
                bool? result = selectWindow.ShowDialog();
                
                if (result == true && selectViewModel.SelectedItem != null)
                {
                    PrototypeId = (ulong)selectViewModel.SelectedItem.Id;
                    Title = selectViewModel.SelectedItem.DisplayName;
                    SetSmartDefaults(selectViewModel.SelectedItem);
                    StatusMessage = "Item selected";
                }
                else
                {
                    StatusMessage = "Item selection cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error selecting item: {ex.Message}";
                Debug.WriteLine($"Error in OpenSelectItemWindowAsync: {ex}");
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
                   PrototypeId > 0 &&
                   !IsSaving;
        }
        
        private async Task SaveAsync()
        {
            // Prevent multiple save operations
            if (!await _saveLock.WaitAsync(0))
            {
                StatusMessage = "Save already in progress";
                return;
            }
            
            try
            {
                // Cancel any previous save operation
                _saveCts?.Cancel();
                _saveCts = new CancellationTokenSource();
                var token = _saveCts.Token;
                
                IsSaving = true;
                StatusMessage = "Saving item...";
                
                // Validate all required fields
                if (!ValidateAllFields(out string validationError))
                {
                    StatusMessage = $"Validation error: {validationError}";
                    MessageBox.Show(validationError, "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create the catalog entry
                var entry = new CatalogEntry
                {
                    SkuId = SkuId,
                    GuidItems = new List<GuidItem>
                    {
                        new GuidItem
                        {
                            PrototypeGuid = 0,
                            ItemPrototypeRuntimeIdForClient = PrototypeId,
                            Quantity = Quantity
                        }
                    },
                    LocalizedEntries = new List<LocalizedEntry>
                    {
                        new LocalizedEntry
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
                
                // Attempt to save with timeout
                var saveTask = _catalogService.SaveItemAsync(entry);
                var completedTask = await Task.WhenAny(saveTask, Task.Delay(10000, token));
                
                if (completedTask != saveTask)
                {
                    throw new TimeoutException("Save operation timed out after 10 seconds");
                }
                
                // Check the actual save result
                bool saveResult = await saveTask;
                
                if (saveResult)
                {
                    // Refresh SKU ID for next item
                    SkuId = await _catalogService.GetNextAvailableSkuId();
                    
                    // Clear or reset other fields as needed
                    Title = string.Empty;
                    Description = string.Empty;
                    PrototypeId = 0;
                    
                    StatusMessage = "Item saved successfully. Ready for next item.";
                    
                    // Force cache refresh in CatalogService
                    await _catalogService.LoadCatalogAsync(true);
                }
                else
                {
                    throw new InvalidOperationException("Save operation returned false");
                }
                
                StatusMessage = "Item saved successfully";
                Debug.WriteLine($"Successfully saved item: {SkuId} - {Title}");
                
                // Close window with success flag
                CloseWindow(true);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Save operation cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving item: {ex.Message}";
                Debug.WriteLine($"Error saving item: {ex}");
                
                MessageBox.Show($"Failed to save item: {ex.Message}\n\nPlease try again or contact support.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
                _saveLock.Release();
            }
        }
        
        private bool ValidateAllFields(out string error)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                error = "Title is required";
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(Description))
            {
                error = "Description is required";
                return false;
            }
            
            if (PrototypeId <= 0)
            {
                error = "Valid Prototype ID is required";
                return false;
            }
            
            if (SkuId <= 0)
            {
                error = "Valid SKU ID is required";
                return false;
            }
            
            error = null;
            return true;
        }
        
        private void Cancel()
        {
            CloseWindow(false);
        }
        
        private void CloseWindow(bool result)
        {
            try
            {
                // Set flag to prevent multiple close attempts
                if (_isClosing)
                    return;
                    
                _isClosing = true;
                StatusMessage = "Closing...";
                
                // Cancel any pending operations
                _saveCts?.Cancel();
                
                // Ensure window closes on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = Application.Current.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this);
                        
                    if (window != null)
                    {
                        window.DialogResult = result;
                        window.Close();
                        Debug.WriteLine($"Window closed successfully with result: {result}");
                    }
                    else
                    {
                        Debug.WriteLine("Warning: Could not find window to close");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error closing window: {ex.Message}");
                
                // Last resort - try force closing
                try
                {
                    var window = Application.Current.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this);
                    if (window != null)
                    {
                        window.Close();
                    }
                }
                catch { /* Ignore any errors in the fallback close */ }
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
