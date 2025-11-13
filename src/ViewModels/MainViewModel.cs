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
using System.Windows.Data;
using System.Windows.Input;
using CatalogManager.Commands;
using CatalogManager.Services;
using CatalogManager.Views;

namespace CatalogManager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _loadCts;
        
        // Use CollectionViewSource for better filtering and sorting
        private readonly CollectionViewSource _itemsViewSource = new CollectionViewSource();
        
        private ObservableCollection<CatalogEntry> _items = new();
        private ObservableCollection<CatalogEntry> _selectedItems = new();
        private string _selectedCategory = "All";
        private string _searchText = "";
        private bool _isLoading;
        private string _statusText;
        private ListSortDirection? _sortDirection;
        private string _sortColumn;
        private int? _minPrice;
        private int? _maxPrice;
        private PriceRange _selectedPriceRange;
        private CatalogEntry _selectedItem;
        private ObservableCollection<string> _categories = new();

        public CatalogService CatalogService => _catalogService;
        
        public ObservableCollection<string> Categories 
        { 
            get => _categories;
            private set => SetProperty(ref _categories, value);
        }
        
        public ICommand RefreshCommand { get; }
        public AsyncRelayCommand LoadItemsCommand { get; }
        public AsyncRelayCommand AddItemCommand { get; }
        public AsyncRelayCommand EditItemCommand { get; }
        public AsyncRelayCommand DeleteItemCommand { get; }
        public AsyncRelayCommand BatchDeleteCommand { get; }
        public AsyncRelayCommand BatchPriceUpdateCommand { get; }
        public AsyncRelayCommand BatchModifyCommand { get; }
        public AsyncRelayCommand CreateBundleCommand { get; }

        private bool _allowCatalogModification;
        public bool AllowCatalogModification
        {
            get => _allowCatalogModification;
            set
            {
                if (value == true)
                {
                    var result = MessageBox.Show(
                        LocalizationService.Instance.GetString("MainWindow.Messages.EnableCatalogDeletionWarning"),
                        LocalizationService.Instance.GetString("MainWindow.Messages.EnableCatalogDeletionTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        SetProperty(ref _allowCatalogModification, value);
                    }
                }
                else
                {
                    SetProperty(ref _allowCatalogModification, value);
                }
            }
        }

        public ICollectionView FilteredItems => _itemsViewSource.View;

        public ObservableCollection<CatalogEntry> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public ObservableCollection<CatalogEntry> SelectedItems 
        {
            get => _selectedItems;
            set => SetProperty(ref _selectedItems, value);
        }
        
        public CatalogEntry SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }
        
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value ?? "All"))
                {
                    LoadItemsAsync().ConfigureAwait(false);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Debounce search input
                    _loadCts?.Cancel();
                    _loadCts = new CancellationTokenSource();
                    var token = _loadCts.Token;
                    
                    Task.Delay(300, token).ContinueWith(t => 
                    {
                        if (!t.IsCanceled)
                        {
                            LoadItemsAsync().ConfigureAwait(false);
                        }
                    }, token);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int? MinPrice
        {
            get => _minPrice;
            set
            {
                if (SetProperty(ref _minPrice, value))
                {
                    LoadItemsAsync().ConfigureAwait(false);
                }
            }
        }

        public int? MaxPrice
        {
            get => _maxPrice;
            set
            {
                if (SetProperty(ref _maxPrice, value))
                {
                    LoadItemsAsync().ConfigureAwait(false);
                }
            }
        }

        public PriceRange SelectedPriceRange
        {
            get => _selectedPriceRange;
            set
            {
                if (SetProperty(ref _selectedPriceRange, value) && value != null)
                {
                    MinPrice = value.Min;
                    MaxPrice = value.Max;
                }
            }
        }

        public ObservableCollection<PriceRange> PriceRanges { get; } = new ObservableCollection<PriceRange>();

        public MainViewModel(CatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _items = new ObservableCollection<CatalogEntry>();
            _itemsViewSource.Source = _items;
            
            // Initialize price ranges with localized names
            InitializePriceRanges();
            
            // Subscribe to language changes to update price ranges
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            
            // Initialize commands
            LoadItemsCommand = new AsyncRelayCommand(LoadItemsAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshItemsAsync);
            AddItemCommand = new AsyncRelayCommand(AddItemAsync);
            EditItemCommand = new AsyncRelayCommand(EditItemAsync, CanEditItem);
            DeleteItemCommand = new AsyncRelayCommand(DeleteItemAsync, CanDeleteItem);
            BatchDeleteCommand = new AsyncRelayCommand(BatchDeleteAsync, CanBatchDelete);
            BatchPriceUpdateCommand = new AsyncRelayCommand(BatchPriceUpdateAsync, CanBatchUpdate);
            BatchModifyCommand = new AsyncRelayCommand(BatchModifyAsync, CanBatchModify);
            CreateBundleCommand = new AsyncRelayCommand(CreateBundleAsync);

            // Set initial status
            StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.NoFilesLoaded");
            
            // Initialize categories with just "All" (translated)
            Categories.Add(LocalizationService.Instance.GetString("Categories.All"));
        }
        
        private void InitializePriceRanges()
        {
            PriceRanges.Clear();
            PriceRanges.Add(new PriceRange { Name = LocalizationService.Instance.GetString("MainWindow.PriceRanges.AllPrices"), Min = null, Max = null });
            PriceRanges.Add(new PriceRange { Name = LocalizationService.Instance.GetString("MainWindow.PriceRanges.Under100"), Min = 0, Max = 100 });
            PriceRanges.Add(new PriceRange { Name = LocalizationService.Instance.GetString("MainWindow.PriceRanges.100to500"), Min = 100, Max = 500 });
            PriceRanges.Add(new PriceRange { Name = LocalizationService.Instance.GetString("MainWindow.PriceRanges.500to1000"), Min = 500, Max = 1000 });
            PriceRanges.Add(new PriceRange { Name = LocalizationService.Instance.GetString("MainWindow.PriceRanges.Over1000"), Min = 1000, Max = null });
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Re-initialize price ranges with new language
            InitializePriceRanges();
            
            // Update category translations
            UpdateCategoryTranslations();
            
            // Update status text if no files are loaded
            if (!_items.Any())
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.NoFilesLoaded");
            }
        }

        private void UpdateCategoryTranslations()
        {
            // Save current selection's English name
            string? currentEnglishCategory = GetEnglishCategoryName(SelectedCategory);
            
            // Rebuild categories with new translations
            var englishCategories = Categories.Select(c => GetEnglishCategoryName(c)).ToList();
            Categories.Clear();
            
            foreach (var englishName in englishCategories)
            {
                Categories.Add(GetTranslatedCategoryName(englishName));
            }
            
            // Restore selection
            if (currentEnglishCategory != null)
            {
                var translatedName = GetTranslatedCategoryName(currentEnglishCategory);
                if (Categories.Contains(translatedName))
                {
                    SelectedCategory = translatedName;
                }
            }
        }

        private string GetEnglishCategoryName(string displayName)
        {
            // Map from display name back to English
            if (displayName == LocalizationService.Instance.GetString("Categories.All")) return "All";
            if (displayName == LocalizationService.Instance.GetString("Categories.Hero")) return "Hero";
            if (displayName == LocalizationService.Instance.GetString("Categories.Costume")) return "Costume";
            if (displayName == LocalizationService.Instance.GetString("Categories.TeamUp")) return "TeamUp";
            if (displayName == LocalizationService.Instance.GetString("Categories.Boost")) return "Boost";
            if (displayName == LocalizationService.Instance.GetString("Categories.Chest")) return "Chest";
            if (displayName == LocalizationService.Instance.GetString("Categories.Service")) return "Service";
            if (displayName == LocalizationService.Instance.GetString("Categories.Bundle")) return "Bundle";
            
            // If no match found, return the original (it's probably already English)
            return displayName;
        }

        private string GetTranslatedCategoryName(string englishName)
        {
            // Map from English to translated
            return englishName switch
            {
                "All" => LocalizationService.Instance.GetString("Categories.All"),
                "Hero" => LocalizationService.Instance.GetString("Categories.Hero"),
                "Costume" => LocalizationService.Instance.GetString("Categories.Costume"),
                "TeamUp" => LocalizationService.Instance.GetString("Categories.TeamUp"),
                "Boost" => LocalizationService.Instance.GetString("Categories.Boost"),
                "Chest" => LocalizationService.Instance.GetString("Categories.Chest"),
                "Service" => LocalizationService.Instance.GetString("Categories.Service"),
                "Bundle" => LocalizationService.Instance.GetString("Categories.Bundle"),
                _ => englishName
            };
        }

        private async Task LoadItemsAsync()
        {
            Debug.WriteLine("LoadItemsAsync: Starting");
            
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadItemsAsync: Operation already in progress");
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.OperationInProgress");
                return;
            }
            
            try
            {
                Debug.WriteLine("LoadItemsAsync: Lock acquired");
                
                // Cancel any previous load operation
                _loadCts?.Cancel();
                _loadCts = new CancellationTokenSource();
                var token = _loadCts.Token;
                
                IsLoading = true;
                StatusText = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadingItems");
                Debug.WriteLine("LoadItemsAsync: Calling CatalogService.LoadCatalogAsync");
                
                // Load catalog data
                var (items, categories) = await _catalogService.LoadCatalogAsync();
                
                Debug.WriteLine($"LoadItemsAsync: Got {items?.Count ?? 0} items and {categories?.Count ?? 0} categories");
                
                // Update categories if we have new categories from the catalog
                if (categories != null && categories.Any())
                {
                    Debug.WriteLine($"LoadItemsAsync: Updating Categories collection with {categories.Count} categories");
                    
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Save the current selection (English name)
                        var currentEnglishCategory = GetEnglishCategoryName(SelectedCategory);
                        
                        Categories.Clear();
                        Categories.Add(GetTranslatedCategoryName("All"));
                        foreach (var category in categories.OrderBy(c => c))
                        {
                            Categories.Add(GetTranslatedCategoryName(category));
                        }
                        Debug.WriteLine($"LoadItemsAsync: Categories collection now has {Categories.Count} items");
                        
                        // Restore the selection if it still exists, otherwise default to "All"
                        var translatedCategory = GetTranslatedCategoryName(currentEnglishCategory);
                        if (Categories.Contains(translatedCategory))
                        {
                            SelectedCategory = translatedCategory;
                        }
                        else
                        {
                            SelectedCategory = GetTranslatedCategoryName("All");
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("LoadItemsAsync: No categories to update");
                }
                
                Debug.WriteLine($"LoadItemsAsync: Calling GetItemsAsync with category='{SelectedCategory}', search='{SearchText}'");
                
                // Convert translated category name back to English for catalog query
                var englishCategory = GetEnglishCategoryName(SelectedCategory);
                
                // Filter items based on current criteria
                var filtered = await _catalogService.GetItemsAsync(
                    englishCategory, 
                    SearchText,
                    token,
                    MinPrice,
                    MaxPrice
                );
                
                Debug.WriteLine($"LoadItemsAsync: GetItemsAsync returned {filtered?.Count() ?? 0} filtered items");
                
                // Apply sorting if needed
                if (_sortColumn != null && _sortDirection.HasValue)
                {
                    Debug.WriteLine($"LoadItemsAsync: Applying sort: column='{_sortColumn}', direction={_sortDirection}");
                    filtered = ApplySorting(filtered, _sortColumn, _sortDirection.Value);
                }
                
                // Update UI on main thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Debug.WriteLine("LoadItemsAsync: Updating Items collection on UI thread");
                    Items.Clear();
                    foreach (var item in filtered)
                    {
                        Items.Add(item);
                    }
                    
                    StatusText = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadedItems", Items.Count);
                    Debug.WriteLine($"LoadItemsAsync: Items collection now has {Items.Count} items");
                    
                    // Refresh the view
                    _itemsViewSource.View.Refresh();
                    Debug.WriteLine("LoadItemsAsync: View refreshed");
                });
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("LoadItemsAsync: Operation cancelled");
                StatusText = LocalizationService.Instance.GetString("SelectItemWindow.Status.LoadingCancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadItemsAsync: Exception caught: {ex}");
                StatusText = LocalizationService.Instance.GetString("SelectItemWindow.Status.ErrorLoading", ex.Message);
                Debug.WriteLine($"Error loading items: {ex}");
            }
            finally
            {
                Debug.WriteLine("LoadItemsAsync: Cleaning up");
                IsLoading = false;
                _operationLock.Release();
                Debug.WriteLine("LoadItemsAsync: Completed");
            }
        }
        
        private async Task RefreshItemsAsync()
        {
            // Force a refresh of the catalog data
            await _catalogService.LoadCatalogAsync(forceRefresh: true);
            await LoadItemsAsync();
        }
        private async Task CreateBundleAsync()
        {
            try
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.OpeningBundleDialog");
                
                var viewModel = new CreateBundleViewModel(_catalogService);
                var window = new CreateBundleWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.BundleCreated");
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.BundleCreationCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorCreatingBundle", ex.Message);
                Debug.WriteLine($"Error in CreateBundleAsync: {ex}");
            }
        }
        private IEnumerable<CatalogEntry> ApplySorting(IEnumerable<CatalogEntry> items, string column, ListSortDirection direction)
        {
            return column switch
            {
                "SkuId" => direction == ListSortDirection.Ascending 
                    ? items.OrderBy(i => i.SkuId) 
                    : items.OrderByDescending(i => i.SkuId),
                    
                "LocalizedEntries[0].Title" => direction == ListSortDirection.Ascending 
                    ? items.OrderBy(i => i.LocalizedEntries[0].Title) 
                    : items.OrderByDescending(i => i.LocalizedEntries[0].Title),
                    
                "Type.Name" => direction == ListSortDirection.Ascending 
                    ? items.OrderBy(i => i.Type.Name) 
                    : items.OrderByDescending(i => i.Type.Name),
                    
                "LocalizedEntries[0].ItemPrice" => direction == ListSortDirection.Ascending 
                    ? items.OrderBy(i => i.LocalizedEntries[0].ItemPrice) 
                    : items.OrderByDescending(i => i.LocalizedEntries[0].ItemPrice),
                    
                _ => items
            };
        }

        private async Task AddItemAsync()
        {
            try
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.OpeningAddDialog");
                
                var viewModel = new AddItemViewModel(_catalogService);
                var window = new AddItemWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ItemAdded");
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.AddItemCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorAddingItem", ex.Message);
                Debug.WriteLine($"Error in AddItemAsync: {ex}");
            }
        }

        private async Task EditItemAsync()
        {
            if (SelectedItems.FirstOrDefault() == null)
            {
                MessageBox.Show(
                    LocalizationService.Instance.GetString("MainWindow.Messages.NoSelectionMessage"),
                    LocalizationService.Instance.GetString("MainWindow.Messages.NoSelectionTitle"),
                    MessageBoxButton.OK);
                return;
            }

            try
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.OpeningEditDialog");
                
                var viewModel = new AddItemViewModel(_catalogService, SelectedItems.First());
                var window = new AddItemWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ItemUpdated");
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.EditItemCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorEditingItem", ex.Message);
                Debug.WriteLine($"Error in EditItemAsync: {ex}");
            }
        }
        
        private bool CanEditItem()
        {
            return SelectedItems?.Count == 1 && !IsLoading;
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItems.FirstOrDefault() == null) return;

            try
            {
                // Add warning about catalog.json items
                var warningMessage = AllowCatalogModification
                    ? LocalizationService.Instance.GetString("MainWindow.Messages.DeleteItemWarning")
                    : LocalizationService.Instance.GetString("MainWindow.Messages.DeleteItemConfirmation");
            
                var result = MessageBox.Show(
                    warningMessage,
                    LocalizationService.Instance.GetString("MainWindow.Messages.DeleteItemTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.DeletingItem");
                    bool success = await _catalogService.DeleteItemAsync(SelectedItems.First().SkuId);

                    if (success)
                    {
                        StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ItemDeleted");
                        await LoadItemsAsync();
                    }
                    else
                    {
                        StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.FailedToDeleteItem");
                        MessageBox.Show(
                            LocalizationService.Instance.GetString("MainWindow.Messages.DeleteItemFailed"),
                            LocalizationService.Instance.GetString("MainWindow.Messages.DeleteItemFailedTitle"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.DeleteCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorDeletingItem", ex.Message);
                Debug.WriteLine($"Error in DeleteItemAsync: {ex}");
            }
        }
        
        private bool CanDeleteItem()
        {
            return SelectedItems?.Count == 1 && !IsLoading;
        }

        private async Task BatchDeleteAsync()
        {
            if (!SelectedItems.Any()) return;

            try
            {
                // Add warning about catalog.json items
                var warningMessage = AllowCatalogModification
                    ? LocalizationService.Instance.GetString("MainWindow.Messages.BatchDeleteWarningCatalog", SelectedItems.Count)
                    : LocalizationService.Instance.GetString("MainWindow.Messages.BatchDeleteConfirmationPatch", SelectedItems.Count);
            
                var result = MessageBox.Show(
                    warningMessage,
                    LocalizationService.Instance.GetString("MainWindow.Messages.BatchDeleteConfirmTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.DeletingItems", SelectedItems.Count);
                    int successCount = 0;
                    
                    foreach (var item in SelectedItems.ToList())
                    {
                        if (await _catalogService.DeleteItemAsync(item.SkuId))
                            successCount++;
                    }
                    
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.DeletedItems", successCount, SelectedItems.Count);
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.BatchDeleteCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorBatchDelete", ex.Message);
                Debug.WriteLine($"Error in BatchDeleteAsync: {ex}");
            }
        }
        
        private bool CanBatchDelete()
        {
            return SelectedItems?.Count > 0 && !IsLoading;
        }

        private async Task BatchPriceUpdateAsync()
        {
            if (!SelectedItems.Any()) return;

            try
            {
                var dialog = new BatchPriceUpdateWindow();
                dialog.Owner = Application.Current.MainWindow;
                
                if (dialog.ShowDialog() == true)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.UpdatingPrices", SelectedItems.Count);
                    int successCount = 0;
                    
                    foreach (var item in SelectedItems.ToList())
                    {
                        try
                        {
                            item.LocalizedEntries[0].ItemPrice = dialog.NewPrice;
                            if (await _catalogService.SaveItemAsync(item))
                                successCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating price for item {item.SkuId}: {ex.Message}");
                        }
                    }
                    
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.PricesUpdated", successCount, SelectedItems.Count);
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.BatchPriceUpdateCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorBatchPriceUpdate", ex.Message);
                Debug.WriteLine($"Error in BatchPriceUpdateAsync: {ex}");
            }
        }
        
        private bool CanBatchUpdate()
        {
            return SelectedItems?.Count > 0 && !IsLoading;
        }

        private async Task BatchModifyAsync()
        {
            if (!SelectedItems.Any()) return;

            try
            {
                // Check if all selected items are of the same type
                var itemTypes = SelectedItems.Select(i => i.Type.Name).Distinct().ToList();
                
                if (itemTypes.Count > 1)
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("MainWindow.Messages.MultipleTypesMessage"),
                        LocalizationService.Instance.GetString("MainWindow.Messages.MultipleTypesTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                string itemType = itemTypes.First();
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.OpeningBatchModifyDialog", itemType);
                
                var viewModel = new BatchModifyViewModel(_catalogService, itemType);
                var window = new BatchModifyWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ModifyingItems", SelectedItems.Count, itemType);
                    int successCount = 0;
                    int alreadyHadCount = 0;
                    int notModifiedCount = 0;
                    
                    foreach (var item in SelectedItems.ToList())
                    {
                        try
                        {
                            // Get the original item from the catalog to ensure we have the complete data
                            var originalItem = await _catalogService.GetItemBySkuIdAsync(item.SkuId);
                            if (originalItem == null) continue;
                            
                            bool modified = false;
                            
                            // Get the current type modifiers
                            var currentModifiers = originalItem.TypeModifiers?.Select(m => m.Name).ToList() ?? new List<string>();
                            
                            // Create a new list of modifiers
                            var newModifiers = new List<string>(currentModifiers);
                            
                            if (viewModel.AddModifiers)
                            {
                                // Add selected modifiers that don't already exist (based on English names)
                                foreach (var modifier in viewModel.SelectedTypeModifiers)
                                {
                                    if (!newModifiers.Contains(modifier.EnglishName))
                                    {
                                        newModifiers.Add(modifier.EnglishName);
                                        modified = true;
                                    }
                                }
                                
                                if (!modified)
                                {
                                    alreadyHadCount++;
                                }
                            }
                            else if (viewModel.RemoveModifiers)
                            {
                                // Remove selected modifiers if they exist (based on English names)
                                foreach (var modifier in viewModel.SelectedTypeModifiers)
                                {
                                    if (newModifiers.Contains(modifier.EnglishName))
                                    {
                                        newModifiers.Remove(modifier.EnglishName);
                                        modified = true;
                                    }
                                }
                                
                                if (!modified)
                                {
                                    notModifiedCount++;
                                }
                            }
                            
                            // Update the item's type modifiers only if changes were made
                            if (modified)
                            {
                                var typeOrder = originalItem.Type?.Order ?? 999;
                                
                                // Create a minimal update object that only contains the SkuId and TypeModifiers
                                var updateItem = new CatalogEntry
                                {
                                    SkuId = originalItem.SkuId,
                                    Type = originalItem.Type,
                                    TypeModifiers = newModifiers
                                        .Select(name => new TypeModifier { Name = name, Order = typeOrder })
                                        .ToList()
                                };
                                
                                // Use a special method to update only the type modifiers
                                if (await _catalogService.UpdateTypeModifiersAsync(updateItem))
                                    successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating item {item.SkuId}: {ex.Message}");
                        }
                    }
                    
                    string statusMessage;
                    if (viewModel.AddModifiers)
                    {
                        statusMessage = LocalizationService.Instance.GetString("MainWindow.StatusBar.ModifiersAdded", 
                            successCount, SelectedItems.Count, itemType, alreadyHadCount);
                    }
                    else
                    {
                        statusMessage = LocalizationService.Instance.GetString("MainWindow.StatusBar.ModifiersRemoved", 
                            successCount, SelectedItems.Count, itemType, notModifiedCount);
                    }
                    
                    StatusText = statusMessage;
                    
                    // Show results popup
                    MessageBox.Show(
                        statusMessage,
                        LocalizationService.Instance.GetString("MainWindow.Messages.BatchModificationResultsTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                        
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.BatchModificationCancelled");
                }
            }
            catch (Exception ex)
            {
                StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.ErrorBatchModification", ex.Message);
                Debug.WriteLine($"Error in BatchModifyAsync: {ex}");
            }
        }


        private bool CanBatchModify()
        {
            return SelectedItems?.Count > 0 && !IsLoading;
        }

        public void OnSorting(string column, ListSortDirection direction)
        {
            _sortColumn = column;
            _sortDirection = direction;
            LoadItemsAsync().ConfigureAwait(false);
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

    public class PriceRange
    {
        public string Name { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
    }
}

