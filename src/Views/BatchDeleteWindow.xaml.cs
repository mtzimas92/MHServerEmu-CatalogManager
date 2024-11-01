using System.Windows;

namespace CatalogManager.Views
{
    public partial class BatchDeleteWindow : Window
    {
        public string DeleteMessage { get; private set; }

        public BatchDeleteWindow(int itemCount)
        {
            InitializeComponent();
            DeleteMessage = $"Are you sure you want to delete {itemCount} selected items?";
            DataContext = this;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}