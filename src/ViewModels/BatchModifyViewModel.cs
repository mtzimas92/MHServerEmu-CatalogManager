using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CatalogManager.Commands;
using CatalogManager.Models;
using CatalogManager.Services;

namespace CatalogManager.ViewModels
{
    public class BatchModifyViewModel : INotifyPropertyChanged
    {
        private readonly CatalogService _catalogService;
        private readonly string _itemType;
        
        private ObservableCollection<LocalizedTypeModifier> _availableTypeModifiers;
        public ObservableCollection<LocalizedTypeModifier> AvailableTypeModifiers
        {
            get => _availableTypeModifiers;
            set => SetProperty(ref _availableTypeModifiers, value);
        }

        private ObservableCollection<LocalizedTypeModifier> _selectedTypeModifiers = new();
        public ObservableCollection<LocalizedTypeModifier> SelectedTypeModifiers
        {
            get => _selectedTypeModifiers;
            set => SetProperty(ref _selectedTypeModifiers, value);
        }
        
        private bool _addModifiers = true;
        public bool AddModifiers
        {
            get => _addModifiers;
            set => SetProperty(ref _addModifiers, value);
        }
        
        private bool _removeModifiers;
        public bool RemoveModifiers
        {
            get => _removeModifiers;
            set => SetProperty(ref _removeModifiers, value);
        }
        
        public string ItemType => _itemType;
        
        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public BatchModifyViewModel(CatalogService catalogService, string itemType)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _itemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
            
            // Initialize available modifiers for this type
            UpdateAvailableModifiers();
            
            ApplyCommand = new RelayCommand(Apply);
            CancelCommand = new RelayCommand(Cancel);
        }
        
        private void UpdateAvailableModifiers()
        {
            try
            {
                var categoryModifiers = _catalogService.GetCategoryModifiers(_itemType);
                AvailableTypeModifiers = new ObservableCollection<LocalizedTypeModifier>(
                    categoryModifiers.Select(m => new LocalizedTypeModifier(m)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating modifiers: {ex.Message}");
                AvailableTypeModifiers = new ObservableCollection<LocalizedTypeModifier>();
            }
        }

        private void Apply()
        {
            DialogResult = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public bool? DialogResult { get; private set; }
        public event EventHandler CloseRequested;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}