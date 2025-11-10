using System.Linq;
using System.Windows;
using CatalogManager.ViewModels;

namespace CatalogManager.Views
{
    public partial class SelectItemWindow : Window
    {
        public SelectItemWindow()
        {
            InitializeComponent();
            Loaded += SelectItemWindow_Loaded;
        }

        private void SelectItemWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Wire up selection changed event to sync with ViewModel
            ItemsListBox.SelectionChanged += ItemsListBox_SelectionChanged;
        }

        private void ItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DataContext is SelectItemViewModel viewModel)
            {
                viewModel.SelectedItems.Clear();
                foreach (var item in ItemsListBox.SelectedItems.Cast<ItemDisplay>())
                {
                    viewModel.SelectedItems.Add(item);
                }
                
                // Notify that HasSelectedItems has changed to update the OK button
                viewModel.OnPropertyChanged(nameof(viewModel.HasSelectedItems));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}