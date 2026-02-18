using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CatalogManager.Services;
using CatalogManager.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace CatalogManager
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(new CatalogService());
            DataContext = _viewModel;

            ItemsDataGrid.SelectionChanged += (s, e) =>
            {
                var vm = (MainViewModel)DataContext;
                vm.SelectedItems.Clear();
                foreach (var item in ItemsDataGrid.SelectedItems)
                {
                    vm.SelectedItems.Add((CatalogEntry)item);
                }
            };
            
            // Subscribe to language changes
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            
            // Populate language selector with available languages
            PopulateLanguageSelector();
        }

        private void PopulateLanguageSelector()
        {
            LanguageComboBox.Items.Clear();
            
            var availableLanguages = LocalizationService.Instance.GetAvailableLanguages();
            var currentLanguage = LocalizationService.Instance.CurrentLanguage;
            
            int selectedIndex = 0;
            for (int i = 0; i < availableLanguages.Count; i++)
            {
                var languageCode = availableLanguages[i];
                var languageName = LocalizationService.Instance.GetLanguageName(languageCode);
                
                var item = new ComboBoxItem
                {
                    Content = languageName,
                    Tag = languageCode
                };
                
                LanguageComboBox.Items.Add(item);
                
                if (languageCode == currentLanguage)
                {
                    selectedIndex = i;
                }
            }
            
            LanguageComboBox.SelectedIndex = selectedIndex;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Refresh the ViewModel's status text to update it to the new language
            if (_viewModel != null)
            {
                var currentStatus = _viewModel.StatusText;
                // Trigger property change notification by reassigning
                _viewModel.StatusText = currentStatus;
            }
        }

        private ListSortDirection? _lastSortDirection;
        private string _lastSortColumn;

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            
            if (DataContext is MainViewModel vm)
            {
                Debug.WriteLine($"Last sort column: {_lastSortColumn}, Last direction: {_lastSortDirection}");
                
                var newDirection = (_lastSortColumn == e.Column.SortMemberPath && _lastSortDirection == ListSortDirection.Ascending)
                    ? ListSortDirection.Descending 
                    : ListSortDirection.Ascending;
                    
                Debug.WriteLine($"New sort direction: {newDirection}");

                var dataGrid = (DataGrid)sender;
                foreach (var col in dataGrid.Columns)
                {
                    if (col != e.Column) 
                    {
                        Debug.WriteLine($"Clearing sort direction for column: {col.Header}");
                        col.SortDirection = null;
                    }
                }
                
                e.Column.SortDirection = newDirection;
                _lastSortDirection = newDirection;
                _lastSortColumn = e.Column.SortMemberPath;
                
                Debug.WriteLine($"Applying sort: Column={e.Column.Header}, Direction={newDirection}");
                
                vm.OnSorting(e.Column.SortMemberPath, newDirection);
            }
        }

        private void ItemsDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // Handle the SelectedItems binding programmatically since it can't be done in XAML
            if (DataContext is MainViewModel viewModel && sender is DataGrid dataGrid)
            {
                dataGrid.SelectionChanged += (s, args) =>
                {
                    viewModel.SelectedItems.Clear();
                    foreach (var item in dataGrid.SelectedItems)
                    {
                        if (item is CatalogEntry entry)
                        {
                            viewModel.SelectedItems.Add(entry);
                        }
                    }
                };
            }
        }

        private async void LoadCatalogFile_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("LoadCatalogFile_Click: Starting catalog file selection");
            
            var dialog = new OpenFileDialog
            {
                Title = "Select Catalog File(s)",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedFiles = dialog.FileNames;
                Logger.Log($"LoadCatalogFile_Click: {selectedFiles.Length} file(s) selected");
                
                try
                {
                    _viewModel.StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.LoadingCatalog");
                    
                    int loadedCount = 0;
                    int failedCount = 0;

                    foreach (var filePath in selectedFiles)
                    {
                        Logger.Log($"LoadCatalogFile_Click: Calling LoadCatalogFileAsync with: {filePath}");
                        bool success = await _viewModel.CatalogService.LoadCatalogFileAsync(filePath);
                        Logger.Log($"LoadCatalogFile_Click: LoadCatalogFileAsync returned: {success} for {filePath}");

                        if (success)
                            loadedCount++;
                        else
                            failedCount++;
                    }

                    if (loadedCount > 0)
                    {
                        var fileCount = _viewModel.CatalogService.LoadedFiles.Count;
                        if (loadedCount == 1)
                        {
                            var fileName = System.IO.Path.GetFileName(selectedFiles[0]);
                            _viewModel.StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.LoadedFiles", fileName, fileCount);
                        }
                        else
                        {
                            _viewModel.StatusText = $"Loaded {loadedCount} files ({fileCount} total loaded)";
                        }
                        Logger.Log("LoadCatalogFile_Click: Executing LoadItemsCommand");
                        // Trigger reload of items once after all files are loaded
                        _viewModel.LoadItemsCommand.Execute(null);
                        Logger.Log("LoadCatalogFile_Click: LoadItemsCommand executed");
                    }

                    if (failedCount > 0)
                    {
                        Logger.Log($"LoadCatalogFile_Click: {failedCount} file(s) failed to load");
                        MessageBox.Show($"{failedCount} file(s) failed to load.", LocalizationService.Instance.GetString("Common.Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException("LoadCatalogFile_Click: Exception caught", ex);
                    MessageBox.Show(LocalizationService.Instance.GetString("MainWindow.StatusBar.Error", ex.Message), LocalizationService.Instance.GetString("Common.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    _viewModel.StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.Error", ex.Message);
                }
            }
            else
            {
                Logger.Log("LoadCatalogFile_Click: File selection cancelled");
            }
        }

        private async void ClearAllFiles_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("ClearAllFiles_Click: Clearing all loaded files");
            
            try
            {
                await _viewModel.CatalogService.ClearAllFilesAsync();
                _viewModel.StatusText = LocalizationService.Instance.GetString("MainWindow.StatusBar.AllFilesCleared");
                Logger.Log("ClearAllFiles_Click: Executing LoadItemsCommand to refresh view");
                _viewModel.LoadItemsCommand.Execute(null);
                Logger.Log("ClearAllFiles_Click: Files cleared successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException("ClearAllFiles_Click: Exception caught", ex);
                MessageBox.Show($"Error clearing files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _viewModel.StatusText = $"Error: {ex.Message}";
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageCode = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(languageCode))
                {
                    LocalizationService.Instance.LoadLanguage(languageCode);
                }
            }
        }
    }
}
