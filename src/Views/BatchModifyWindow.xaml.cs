using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CatalogManager.Models;
using CatalogManager.ViewModels;

namespace CatalogManager.Views
{
    public partial class BatchModifyWindow : Window
    {
        public BatchModifyWindow()
        {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            
            if (DataContext is BatchModifyViewModel viewModel)
            {
                viewModel.CloseRequested += (sender, args) =>
                {
                    // Get selected modifiers from the ListBox
                    var selectedModifiers = new List<LocalizedTypeModifier>();
                    foreach (var item in TypeModifiersListBox.SelectedItems)
                    {
                        if (item is LocalizedTypeModifier modifier)
                        {
                            selectedModifiers.Add(modifier);
                        }
                    }
                    viewModel.SelectedTypeModifiers = new System.Collections.ObjectModel.ObservableCollection<LocalizedTypeModifier>(selectedModifiers);
                    
                    DialogResult = viewModel.DialogResult;
                    Close();
                };
            }
        }
    }
}