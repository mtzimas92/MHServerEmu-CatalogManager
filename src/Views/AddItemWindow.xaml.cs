using System.Windows;
using System.Windows.Controls;
using CatalogManager.ViewModels;
namespace CatalogManager.Views
{
    public partial class AddItemWindow : Window
    {
        public AddItemWindow()
        {
            InitializeComponent();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AddItemViewModel viewModel)
            {
                viewModel.UpdateSelectedModifiers(sender as ListBox);
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddItemViewModel viewModel)
            {
                viewModel.UpdateListBoxSelections();
            }
        }
    }
}
