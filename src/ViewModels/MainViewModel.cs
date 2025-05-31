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

        public ObservableCollection<PriceRange> PriceRanges { get; } = new ObservableCollection<PriceRange>
        {
            new PriceRange { Name = "All Prices", Min = null, Max = null },
            new PriceRange { Name = "Under 100", Min = 0, Max = 100 },
            new PriceRange { Name = "100-500", Min = 100, Max = 500 },
            new PriceRange { Name = "500-1000", Min = 500, Max = 1000 },
            new PriceRange { Name = "Over 1000", Min = 1000, Max = null }
        };

        public MainViewModel(CatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _items = new ObservableCollection<CatalogEntry>();
            _itemsViewSource.Source = _items;
            
            // Initialize commands
            LoadItemsCommand = new AsyncRelayCommand(LoadItemsAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshItemsAsync);
            AddItemCommand = new AsyncRelayCommand(AddItemAsync);
            EditItemCommand = new AsyncRelayCommand(EditItemAsync, CanEditItem);
            DeleteItemCommand = new AsyncRelayCommand(DeleteItemAsync, CanDeleteItem);
            BatchDeleteCommand = new AsyncRelayCommand(BatchDeleteAsync, CanBatchDelete);
            BatchPriceUpdateCommand = new AsyncRelayCommand(BatchPriceUpdateAsync, CanBatchUpdate);
            BatchModifyCommand = new AsyncRelayCommand(BatchModifyAsync, CanBatchModify);


            // Initial load
            LoadItemsAsync().ConfigureAwait(false);
        }

        private async Task LoadItemsAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                StatusText = "Operation already in progress";
                return;
            }
            
            try
            {
                // Cancel any previous load operation
                _loadCts?.Cancel();
                _loadCts = new CancellationTokenSource();
                var token = _loadCts.Token;
                
                IsLoading = true;
                StatusText = "Loading items...";
                
                // Load catalog data
                var (items, categories) = await _catalogService.LoadCatalogAsync();
                
                // Update categories if needed
                if (!Categories.Any())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Categories.Add("All");
                        foreach (var category in categories.OrderBy(c => c))
                        {
                            Categories.Add(category);
                        }
                    });
                }
                
                // Filter items based on current criteria
                var filtered = await _catalogService.GetItemsAsync(
                    SelectedCategory, 
                    SearchText,
                    token,
                    MinPrice,
                    MaxPrice
                );
                
                // Apply sorting if needed
                if (_sortColumn != null && _sortDirection.HasValue)
                {
                    filtered = ApplySorting(filtered, _sortColumn, _sortDirection.Value);
                }
                
                // Update UI on main thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Items.Clear();
                    foreach (var item in filtered)
                    {
                        Items.Add(item);
                    }
                    
                    StatusText = $"Loaded {Items.Count} items";
                    
                    // Refresh the view
                    _itemsViewSource.View.Refresh();
                });
            }
            catch (OperationCanceledException)
            {
                StatusText = "Loading cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading items: {ex.Message}";
                Debug.WriteLine($"Error loading items: {ex}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        
        private async Task RefreshItemsAsync()
        {
            // Force a refresh of the catalog data
            await _catalogService.LoadCatalogAsync(forceRefresh: true);
            await LoadItemsAsync();
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
                StatusText = "Opening add item dialog...";
                
                var viewModel = new AddItemViewModel(_catalogService);
                var window = new AddItemWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = "Item added successfully";
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = "Add item cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error adding item: {ex.Message}";
                Debug.WriteLine($"Error in AddItemAsync: {ex}");
            }
        }

        private async Task EditItemAsync()
        {
            if (SelectedItems.FirstOrDefault() == null)
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", MessageBoxButton.OK);
                return;
            }

            try
            {
                StatusText = "Opening edit item dialog...";
                
                var viewModel = new AddItemViewModel(_catalogService, SelectedItems.First());
                var window = new AddItemWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = "Item updated successfully";
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = "Edit item cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error editing item: {ex.Message}";
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
                var warningMessage = 
                    $"Are you sure you want to delete '{SelectedItems.First().LocalizedEntries[0].Title}'?\n\n" +
                    "Note: Items that exist in the original catalog.json file cannot be deleted via patch. " +
                    "Only items added through patches can be removed.";
            
                var result = MessageBox.Show(
                    warningMessage,
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StatusText = "Deleting item...";
                    bool success = await _catalogService.DeleteFromPatchAsync(SelectedItems.First().SkuId);
                    
                    if (success)
                    {
                        StatusText = "Item deleted successfully";
                        await LoadItemsAsync();
                    }
                    else
                    {
                        StatusText = "Failed to delete item";
                        MessageBox.Show("The item could not be deleted. Items that exist in the original catalog.json file cannot be deleted via patch.",
                            "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    StatusText = "Delete cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error deleting item: {ex.Message}";
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
                var warningMessage = 
                    $"Are you sure you want to delete {SelectedItems.Count} items?\n\n" +
                    "Note: Items that exist in the original catalog.json file cannot be deleted via patch. " +
                    "Only items added through patches can be removed.";
            
                var result = MessageBox.Show(
                    warningMessage,
                    "Confirm Batch Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StatusText = $"Deleting {SelectedItems.Count} items...";
                    int successCount = 0;
                    
                    foreach (var item in SelectedItems.ToList())
                    {
                        if (await _catalogService.DeleteFromPatchAsync(item.SkuId))
                            successCount++;
                    }
                    
                    StatusText = $"Deleted {successCount} of {SelectedItems.Count} items";
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = "Batch delete cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error during batch delete: {ex.Message}";
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
                    StatusText = $"Updating prices for {SelectedItems.Count} items...";
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
                    
                    StatusText = $"Updated prices for {successCount} of {SelectedItems.Count} items";
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = "Batch price update cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error during batch price update: {ex.Message}";
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
                        "You can only batch modify items of the same type. Please filter your selection to include only one type of item.",
                        "Multiple Types Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                string itemType = itemTypes.First();
                StatusText = $"Opening batch modify dialog for {itemType} items...";
                
                var viewModel = new BatchModifyViewModel(_catalogService, itemType);
                var window = new BatchModifyWindow { DataContext = viewModel };
                window.Owner = Application.Current.MainWindow;
                
                if (window.ShowDialog() == true)
                {
                    StatusText = $"Modifying {SelectedItems.Count} {itemType} items...";
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
                                // Add selected modifiers that don't already exist
                                foreach (var modifier in viewModel.SelectedTypeModifiers)
                                {
                                    if (!newModifiers.Contains(modifier))
                                    {
                                        newModifiers.Add(modifier);
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
                                // Remove selected modifiers if they exist
                                foreach (var modifier in viewModel.SelectedTypeModifiers)
                                {
                                    if (newModifiers.Contains(modifier))
                                    {
                                        newModifiers.Remove(modifier);
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
                        statusMessage = $"Added modifiers to {successCount} of {SelectedItems.Count} {itemType} items. {alreadyHadCount} items already had all selected modifiers.";
                    }
                    else
                    {
                        statusMessage = $"Removed modifiers from {successCount} of {SelectedItems.Count} {itemType} items. {notModifiedCount} items didn't have the selected modifiers.";
                    }
                    
                    StatusText = statusMessage;
                    await LoadItemsAsync();
                }
                else
                {
                    StatusText = "Batch modification cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error during batch modification: {ex.Message}";
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

