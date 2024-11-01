using System.Windows;
using System.Windows.Controls;

namespace CatalogManager.Views
{
    public partial class BatchPriceUpdateWindow : Window
    {
        public int NewPrice { get; private set; }

        public BatchPriceUpdateWindow()
        {
            InitializeComponent();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PriceTextBox.Text, out int price))
            {
                NewPrice = price;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Invalid Input");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
