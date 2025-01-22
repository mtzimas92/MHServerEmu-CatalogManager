using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CatalogManager.Commands;
using CatalogManager.Services;
using CatalogManager.Views;
using System.Diagnostics;
using System.Windows.Input;

namespace CatalogManager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
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

        public CatalogService CatalogService => _catalogService;
        public ObservableCollection<string> Categories { get; private set; } = new ObservableCollection<string>();
        public ICommand RefreshCommand { get; }
        //public ICommand AnalyzeItemCategoriesCommand { get; }
        //public ICommand AnalyzeDesignStatesCommand { get; }

        public AsyncRelayCommand LoadItemsCommand { get; }
        public AsyncRelayCommand AddItemCommand { get; }
        public AsyncRelayCommand EditItemCommand { get; }
        public AsyncRelayCommand DeleteItemCommand { get; }
        public ICommand BatchDeleteCommand { get; private set; }
        public ICommand BatchPriceUpdateCommand { get; private set; }

        public ObservableCollection<CatalogEntry> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<CatalogEntry> SelectedItems 
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value;
                OnPropertyChanged();
            }
        }
        public CatalogEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value ?? "All";
                    OnPropertyChanged();
                    _ = LoadItemsAsync();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _ = LoadItemsAsync();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public int? MinPrice
        {
            get => _minPrice;
            set
            {
                _minPrice = value;
                OnPropertyChanged();
                _ = LoadItemsAsync();
            }
        }

        public int? MaxPrice
        {
            get => _maxPrice;
            set
            {
                _maxPrice = value;
                OnPropertyChanged();
                _ = LoadItemsAsync();
            }
        }

        public PriceRange SelectedPriceRange
        {
            get => _selectedPriceRange;
            set
            {
                _selectedPriceRange = value;
                if (value != null)
                {
                    MinPrice = value.Min;
                    MaxPrice = value.Max;
                }
                OnPropertyChanged();
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
            _catalogService = catalogService;
            _items = new ObservableCollection<CatalogEntry>();
            Categories = new ObservableCollection<string>();

            LoadItemsCommand = new AsyncRelayCommand(LoadItemsAsync);
            RefreshCommand = new AsyncRelayCommand(LoadItemsAsync);
            AddItemCommand = new AsyncRelayCommand(AddItemAsync);
            EditItemCommand = new AsyncRelayCommand(EditItemAsync);
            DeleteItemCommand = new AsyncRelayCommand(DeleteItemAsync);
            BatchDeleteCommand = new AsyncRelayCommand(BatchDeleteAsync);
            BatchPriceUpdateCommand = new AsyncRelayCommand(BatchPriceUpdateAsync);

            _ = LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            IsLoading = true;
            try
            {
                var (items, categories) = await _catalogService.LoadCatalogAsync();
                
                // Only update Categories if they're empty
                if (!Categories.Any())
                {
                    Categories.Add("All");
                    foreach (var category in categories.OrderBy(c => c))
                    {
                        Categories.Add(category);
                    }
                }

                var filtered = await _catalogService.GetItemsAsync(
                    SelectedCategory, 
                    SearchText,
                    MinPrice,  // Pass the MinPrice to the filter
                    MaxPrice   // Pass the MaxPrice to the filter
                );

                if (_sortColumn != null && _sortDirection.HasValue)
                {
                    var sortedItems = _sortColumn switch
                    {
                        "SkuId" => _sortDirection == ListSortDirection.Ascending 
                            ? filtered.OrderBy(i => i.SkuId) 
                            : filtered.OrderByDescending(i => i.SkuId),
                        "LocalizedEntries[0].Title" => _sortDirection == ListSortDirection.Ascending 
                            ? filtered.OrderBy(i => i.LocalizedEntries[0].Title) 
                            : filtered.OrderByDescending(i => i.LocalizedEntries[0].Title),
                        "Type.Name" => _sortDirection == ListSortDirection.Ascending 
                            ? filtered.OrderBy(i => i.Type.Name) 
                            : filtered.OrderByDescending(i => i.Type.Name),
                        "LocalizedEntries[0].ItemPrice" => _sortDirection == ListSortDirection.Ascending 
                            ? filtered.OrderBy(i => i.LocalizedEntries[0].ItemPrice) 
                            : filtered.OrderByDescending(i => i.LocalizedEntries[0].ItemPrice),
                        _ => filtered
                    };
                    Items = new ObservableCollection<CatalogEntry>(sortedItems);
                }
                else
                {
                    Items = new ObservableCollection<CatalogEntry>(filtered);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }


        private async Task AddItemAsync()
        {
            var viewModel = new AddItemViewModel(_catalogService);
            var window = new AddItemWindow { DataContext = viewModel };
            
            if (window.ShowDialog() == true)
            {
                await LoadItemsAsync();
            }
        }

        private async Task EditItemAsync()
        {
            if (SelectedItems.FirstOrDefault() == null)
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", MessageBoxButton.OK);
                return;
            }

            var viewModel = new AddItemViewModel(_catalogService, SelectedItems.First());
            var window = new AddItemWindow { DataContext = viewModel };
            
            if (window.ShowDialog() == true)
            {
                await LoadItemsAsync();
            }
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItems.FirstOrDefault() == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{SelectedItems.First().LocalizedEntries[0].Title}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _catalogService.DeleteFromPatchAsync(SelectedItems.First().SkuId);
                await LoadItemsAsync();
            }
        }

        private async Task BatchDeleteAsync()
        {
            if (!SelectedItems.Any()) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete {SelectedItems.Count} items?",
                "Confirm Batch Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in SelectedItems.ToList())
                {
                    await _catalogService.DeleteFromPatchAsync(item.SkuId);
                }
                await LoadItemsAsync();
            }
        }

        private async Task BatchPriceUpdateAsync()
        {
            if (!SelectedItems.Any()) return;

            var dialog = new BatchPriceUpdateWindow();
            if (dialog.ShowDialog() == true)
            {
                foreach (var item in SelectedItems.ToList())
                {
                    item.LocalizedEntries[0].ItemPrice = dialog.NewPrice;
                    await _catalogService.SaveItemAsync(item);
                }
                await LoadItemsAsync();
            }
        }

        public void OnSorting(string column, ListSortDirection direction)
        {
            _sortColumn = column;
            _sortDirection = direction;
            _ = LoadItemsAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
