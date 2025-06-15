using System.Windows;
using System.Windows.Controls;
using CatalogManager.ViewModels;

namespace CatalogManager.Views
{
    public partial class CreateBundleWindow : Window
    {
        public CreateBundleWindow()
        {
            InitializeComponent();
            Loaded += CreateBundleWindow_Loaded;
        }

        private void CreateBundleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CreateBundleViewModel viewModel)
            {
                viewModel.UpdateListBoxSelections();
            }
        }

        private void TypeModifiersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is CreateBundleViewModel viewModel && sender is ListBox listBox)
            {
                viewModel.UpdateSelectedModifiers(listBox);
            }
        }
    }
}