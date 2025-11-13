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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CatalogManager.Commands;
using CatalogManager.Models;
using CatalogManager.Services;
using CatalogManager.Views;
using MHServerEmu.Games.GameData;

namespace CatalogManager.ViewModels
{
    public class BundleItemEntry
    {
        public ItemDisplay Item { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class CreateBundleViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _saveCts;
        private readonly HtmlGeneratorService _htmlGenerator;

        // Command properties
        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand AddBonusItemCommand { get; }
        public ICommand RemoveBonusItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        
        // State tracking
        private bool _isClosing;
        private bool _isSaving;
        private string _statusMessage;
        
        // UI properties
        public string WindowTitle => IsBogo 
            ? LocalizationService.Instance.GetString("CreateBundleWindow.TitleBOGO") 
            : LocalizationService.Instance.GetString("CreateBundleWindow.TitleBundle");
        
        private bool _isBogo;
        public bool IsBogo
        {
            get => _isBogo;
            set
            {
                if (SetProperty(ref _isBogo, value))
                {
                    // Update type and other properties based on BOGO status
                    if (value)
                    {
                        SelectedType = FindItemTypeByEnglishName("Hero"); // BOGOs are typically Hero type
                        // Add default modifiers for BOGO
                        if (!SelectedTypeModifiers.Any(m => m.EnglishName == "NoDisplay"))
                            SelectedTypeModifiers.Add(new LocalizedTypeModifier("NoDisplay"));
                        if (!SelectedTypeModifiers.Any(m => m.EnglishName == "NoDisplayStore"))
                            SelectedTypeModifiers.Add(new LocalizedTypeModifier("NoDisplayStore"));
                    }
                    else
                    {
                        SelectedType = FindItemTypeByEnglishName("Bundle");
                    }
                    
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(BonusItemsVisibility));
                }
            }
        }
        
        public Visibility BonusItemsVisibility => IsBogo ? Visibility.Visible : Visibility.Collapsed;
        
        private ulong _skuId;
        public ulong SkuId
        {
            get => _skuId;
            set => SetProperty(ref _skuId, value);
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
        
        private ObservableCollection<LocalizedTypeModifier> _availableTypeModifiers;
        public ObservableCollection<LocalizedTypeModifier> AvailableTypeModifiers
        {
            get => _availableTypeModifiers;
            set => SetProperty(ref _availableTypeModifiers, value);
        }

        private ObservableCollection<LocalizedTypeModifier> _selectedTypeModifiers = new();
        public ObservableCollection<LocalizedTypeModifier> SelectedTypeModifiers
        {
            get => _selectedTypeModifiers;
            set => SetProperty(ref _selectedTypeModifiers, value);
        }        private ObservableCollection<BundleItemEntry> _bundleItems = new();
        public ObservableCollection<BundleItemEntry> BundleItems
        {
            get => _bundleItems;
            set => SetProperty(ref _bundleItems, value);
        }
        
        private BundleItemEntry _selectedBundleItem;
        public BundleItemEntry SelectedBundleItem
        {
            get => _selectedBundleItem;
            set => SetProperty(ref _selectedBundleItem, value);
        }
        
        private ObservableCollection<BundleItemEntry> _bonusItems = new();
        public ObservableCollection<BundleItemEntry> BonusItems
        {
            get => _bonusItems;
            set => SetProperty(ref _bonusItems, value);
        }
        

        private BundleItemEntry _selectedBonusItem;
        public BundleItemEntry SelectedBonusItem
        {
            get => _selectedBonusItem;
            set => SetProperty(ref _selectedBonusItem, value);
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

        private ObservableCollection<LocalizedItemType> _itemTypes;
        public ObservableCollection<LocalizedItemType> ItemTypes 
        {
            get => _itemTypes;
            private set => SetProperty(ref _itemTypes, value);
        }
        
        private LocalizedItemType _selectedType;
        public LocalizedItemType SelectedType
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

        public CreateBundleViewModel(CatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _htmlGenerator = new HtmlGeneratorService(catalogService);

            // Initialize commands
            AddItemCommand = new AsyncRelayCommand(AddItemAsync);
            RemoveItemCommand = new RelayCommand(RemoveItem, CanRemoveItem);
            AddBonusItemCommand = new AsyncRelayCommand(AddBonusItemAsync);
            RemoveBonusItemCommand = new RelayCommand(RemoveBonusItem, CanRemoveBonusItem);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(Cancel);
            
            // Initialize item types with localized names
            _itemTypes = new ObservableCollection<LocalizedItemType>
            {
                new LocalizedItemType("Bundle"),
                new LocalizedItemType("Hero"),
                new LocalizedItemType("Costume"),
                new LocalizedItemType("TeamUp"),
                new LocalizedItemType("Boost"),
                new LocalizedItemType("Chest"),
                new LocalizedItemType("Service")
            };
            
            // Initialize the view model
            InitializeNewBundleAsync().ConfigureAwait(false);
        }
        
        private LocalizedItemType FindItemTypeByEnglishName(string englishName)
        {
            return ItemTypes.FirstOrDefault(t => t.EnglishName == englishName);
        }
        
        private async Task InitializeNewBundleAsync()
        {
            try
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.Initializing");
                SkuId = await _catalogService.GetNextAvailableSkuId();
                SelectedType = FindItemTypeByEnglishName("Bundle"); // Default type
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.Ready");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.ErrorInitializing", ex.Message);
                Debug.WriteLine($"Error initializing new bundle: {ex}");
            }
        }
        
        private void UpdateAvailableModifiers()
        {
            try
            {
                var categoryModifiers = _catalogService.GetCategoryModifiers(SelectedType?.EnglishName ?? "Bundle");
                AvailableTypeModifiers = new ObservableCollection<LocalizedTypeModifier>(
                    categoryModifiers.Select(m => new LocalizedTypeModifier(m)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating modifiers: {ex.Message}");
                AvailableTypeModifiers = new ObservableCollection<LocalizedTypeModifier>();
            }
        }
        
        private List<TypeModifier> GetTypeModifiersForBundle()
        {
            var typeOrder = 999;
            // Get English names from selected type modifiers to save to catalog
            return SelectedTypeModifiers
                .Select(m => new TypeModifier { Name = m.EnglishName, Order = typeOrder })
                .ToList();
        }
        
        private async Task AddItemAsync()
        {
            try
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.OpeningSelector");
                
                var selectViewModel = new SelectItemViewModel(_catalogService);
                var selectWindow = new SelectItemWindow
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = selectViewModel
                };

                bool? result = selectWindow.ShowDialog();
                
                if (result == true && selectViewModel.SelectedItems != null && selectViewModel.SelectedItems.Any())
                {
                    // Add all selected items to the bundle
                    foreach (var selectedItem in selectViewModel.SelectedItems)
                    {
                        BundleItems.Add(new BundleItemEntry 
                        { 
                            Item = selectedItem,
                            Quantity = 1 
                        });
                    }
                    
                    int count = selectViewModel.SelectedItems.Count;
                    StatusMessage = count == 1 ? LocalizationService.Instance.GetString("CreateBundleWindow.Status.ItemAdded") : LocalizationService.Instance.GetString("CreateBundleWindow.Status.ItemsAdded", count);
                }
                else
                {
                    StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.SelectionCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.ErrorSelecting", ex.Message);
                Debug.WriteLine($"Error in AddItemAsync: {ex}");
            }
        }
        
        private void RemoveItem()
        {
            if (SelectedBundleItem != null)
            {
                BundleItems.Remove(SelectedBundleItem);
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.ItemRemoved");
            }
        }
        
        private bool CanRemoveItem()
        {
            return SelectedBundleItem != null;
        }
        
        private async Task AddBonusItemAsync()
        {
            if (!IsBogo) return;
            
            try
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.OpeningSelectorBonus");
                
                var selectViewModel = new SelectItemViewModel(_catalogService);
                var selectWindow = new SelectItemWindow
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = selectViewModel
                };

                bool? result = selectWindow.ShowDialog();
                
                if (result == true && selectViewModel.SelectedItems != null && selectViewModel.SelectedItems.Any())
                {
                    // Add all selected items to bonus items
                    foreach (var selectedItem in selectViewModel.SelectedItems)
                    {
                        BonusItems.Add(new BundleItemEntry 
                        { 
                            Item = selectedItem,
                            Quantity = 1 
                        });
                    }
                    
                    int count = selectViewModel.SelectedItems.Count;
                    StatusMessage = count == 1 ? LocalizationService.Instance.GetString("CreateBundleWindow.Status.BonusAdded") : LocalizationService.Instance.GetString("CreateBundleWindow.Status.BonusItemsAdded", count);
                }
                else
                {
                    StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.BonusSelectionCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.ErrorSelectingBonus", ex.Message);
                Debug.WriteLine($"Error in AddBonusItemAsync: {ex}");
            }
        }
        
        private void RemoveBonusItem()
        {
            if (SelectedBonusItem != null)
            {
                BonusItems.Remove(SelectedBonusItem);
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.BonusRemoved");
            }
        }
        
        private bool CanRemoveBonusItem()
        {
            return SelectedBonusItem != null;
        }
        
        private bool CanSave()
        {
            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
                return false;
                
            if (BundleItems.Count == 0)
                return false;
                
            if (IsBogo && BonusItems.Count == 0)
                return false;
                
            return !IsSaving;
        }
        
        private async Task SaveAsync()
        {
            // Prevent multiple save operations
            if (!await _saveLock.WaitAsync(0))
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.SaveInProgress");
                return;
            }
            
            try
            {
                // Cancel any previous save operation
                _saveCts?.Cancel();
                _saveCts = new CancellationTokenSource();
                var token = _saveCts.Token;
                
                IsSaving = true;
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.Saving");
                
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
                    GuidItems = BundleItems.Select(item => new GuidItem
                    {
                        PrototypeGuid = 0,
                        ItemPrototypeRuntimeIdForClient = (ulong)item.Item.Id,
                        Quantity = item.Quantity
                    }).ToList(),
                    AdditionalGuidItems = IsBogo ? BonusItems.Select(item => new GuidItem
                    {
                        PrototypeGuid = 0,
                        ItemPrototypeRuntimeIdForClient = (ulong)item.Item.Id,
                        Quantity = item.Quantity
                    }).ToList() : new List<GuidItem>(),
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
                    InfoUrls = IsBogo ? new List<InfoUrl>() : new List<InfoUrl>
                    {
                        new InfoUrl
                        {
                            LanguageId = "en_us",
                            Url = $"http://storecdn.marvelheroes.com/cdn/en_us/bundles/{Title.ToLower().Replace(" ", "_")}_en_bundle.html",
                            ImageData = ""
                        }
                    },
                    ContentData = IsBogo ? new List<ContentData>() : new List<ContentData>
                    {
                        new ContentData
                        {
                            LanguageId = "en_us",
                            Url = $"http://storecdn.marvelheroes.com/bundles/MTX_Store_Bundle_{Title.Replace(" ", "-")}_Thumb.png",
                            ImageData = ""
                        }
                    },
                    Type = new ItemType
                    {
                        Name = SelectedType.EnglishName,
                        Order = IsBogo ? 0 : 5
                    },
                    TypeModifiers = IsBogo
                        ? SelectedTypeModifiers.Select(m => new TypeModifier { Name = m.EnglishName, Order = 0 }).ToList()
                        : SelectedTypeModifiers.Select(m => new TypeModifier { Name = m.EnglishName, Order = 5 }).ToList()
                };
                if (!IsBogo)
                {
                    StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.GeneratingHtml");
                    
                    try
                    {
                        // Generate HTML file
                        await _htmlGenerator.GenerateBundleHtmlAsync(entry);
                        
                        // Generate thumbnail image
                        await _htmlGenerator.GenerateThumbnailImageAsync(Title);
                        
                        StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.HtmlGenerated");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error generating HTML/thumbnail: {ex.Message}");
                        StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.HtmlWarning");
                    }
                }
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
                    // Refresh SKU ID for next bundle
                    SkuId = await _catalogService.GetNextAvailableSkuId();
                    
                    // Force cache refresh in CatalogService
                    await _catalogService.LoadCatalogAsync(true);
                    
                    StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.SavedNext");
                }
                Debug.WriteLine($"Successfully saved bundle: {SkuId} - {Title}");
                
                // Close window with success flag
                CloseWindow(true);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.SaveCancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.ErrorSaving", ex.Message);
                Debug.WriteLine($"Error saving bundle: {ex}");
                
                MessageBox.Show($"Failed to save bundle: {ex.Message}\n\nPlease try again or contact support.",
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
                error = LocalizationService.Instance.GetString("CreateBundleWindow.Validation.TitleRequired");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(Description))
            {
                error = LocalizationService.Instance.GetString("CreateBundleWindow.Validation.DescriptionRequired");
                return false;
            }
            
            if (BundleItems.Count == 0)
            {
                error = LocalizationService.Instance.GetString("CreateBundleWindow.Validation.AtLeastOneItem");
                return false;
            }
            
            if (IsBogo && BonusItems.Count == 0)
            {
                error = LocalizationService.Instance.GetString("CreateBundleWindow.Validation.AtLeastOneBonus");
                return false;
            }
            
            if (SkuId <= 0)
            {
                error = LocalizationService.Instance.GetString("CreateBundleWindow.Validation.ValidSkuRequired");
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
                StatusMessage = LocalizationService.Instance.GetString("CreateBundleWindow.Status.Closing");
                
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
        
        private void UpdateListBoxItemsSelection(ListBox listBox, List<LocalizedTypeModifier> selectedModifiers)
        {
            if (listBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    var item = listBox.Items[i] as LocalizedTypeModifier;
                    var listBoxItem = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (listBoxItem != null && item != null)
                    {
                        listBoxItem.IsSelected = selectedModifiers.Any(m => m.EnglishName == item.EnglishName);
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
                if (item is LocalizedTypeModifier modifier)
                {
                    SelectedTypeModifiers.Add(modifier);
                }
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
}


