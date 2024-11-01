using System.Windows;
using System.Windows.Controls;
using CatalogManager.Services;
using CatalogManager.ViewModels;
using System.ComponentModel;
using System.Diagnostics;


namespace CatalogManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
        InitializeComponent();
        DataContext = new MainViewModel(new CatalogService());

        ItemsGrid.SelectionChanged += (s, e) =>
        {
            var vm = (MainViewModel)DataContext;
            vm.SelectedItems.Clear();
            foreach (var item in ItemsGrid.SelectedItems)
            {
                vm.SelectedItems.Add((CatalogEntry)item);
            }
        };
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


    }
}
