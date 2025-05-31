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
            Loaded += AddItemWindow_Loaded;
        }

        private void AddItemWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddItemViewModel viewModel)
            {
                viewModel.UpdateListBoxSelections();
            }
        }

        private void TypeModifiersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AddItemViewModel viewModel && sender is ListBox listBox)
            {
                viewModel.UpdateSelectedModifiers(listBox);
            }
        }
    }
}
