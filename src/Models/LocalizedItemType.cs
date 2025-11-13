using System.ComponentModel;
using CatalogManager.Services;

namespace CatalogManager.Models
{
    /// <summary>
    /// Represents an item type with localized display name
    /// EnglishName is used for catalog operations (Hero, Costume, etc.)
    /// DisplayName shows the translated name in the UI
    /// </summary>
    public class LocalizedItemType : INotifyPropertyChanged
    {
        private string _displayName;

        public string EnglishName { get; set; }

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

        public LocalizedItemType(string englishName)
        {
            EnglishName = englishName;
            
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
            // Map to Categories section in localization JSON
            var key = $"Categories.{EnglishName}";
            DisplayName = LocalizationService.Instance.GetString(key);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => DisplayName;
    }
}
