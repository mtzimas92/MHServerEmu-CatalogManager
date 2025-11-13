using System.ComponentModel;
using CatalogManager.Services;

namespace CatalogManager.Models
{
    /// <summary>
    /// Represents a category with localized display name
    /// EnglishName is used for filtering/catalog operations
    /// DisplayName shows the translated name in the UI
    /// </summary>
    public class LocalizedCategory : INotifyPropertyChanged
    {
        private string _displayName;

        public string Path { get; set; }
        public string EnglishName { get; set; }
        public bool IsInventoryType { get; set; }

        public string DisplayName
        {
            get => _displayName;
            private set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LocalizedCategory(string path, string englishName, bool isInventoryType)
        {
            Path = path;
            EnglishName = englishName;
            IsInventoryType = isInventoryType;
            
            UpdateDisplayName();
            
            // Subscribe to language changes
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, System.EventArgs e)
        {
            UpdateDisplayName();
        }

        private void UpdateDisplayName()
        {
            // Try to get localized name from ItemCategories section
            var key = $"ItemCategories.{EnglishName.Replace(" ", "")}";
            DisplayName = LocalizationService.Instance.GetString(key);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
