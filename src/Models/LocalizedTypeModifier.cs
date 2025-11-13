using System.ComponentModel;
using System.Runtime.CompilerServices;
using CatalogManager.Services;

namespace CatalogManager.Models
{
    /// <summary>
    /// Wrapper class for type modifiers that displays translated names in UI
    /// but saves English names to the catalog
    /// </summary>
    public class LocalizedTypeModifier : INotifyPropertyChanged
    {
        private string _displayName;

        /// <summary>
        /// The English name that gets saved to the catalog (never changes)
        /// </summary>
        public string EnglishName { get; set; }

        /// <summary>
        /// The translated name displayed in the UI (updates on language change)
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public LocalizedTypeModifier(string englishName)
        {
            EnglishName = englishName;
            RefreshDisplayName();
            
            // Subscribe to language changes
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, System.EventArgs e)
        {
            RefreshDisplayName();
        }

        private void RefreshDisplayName()
        {
            // Try to get translation, fall back to English if not found
            var translated = LocalizationService.Instance.GetString($"TypeModifiers.{EnglishName}");
            DisplayName = translated.StartsWith("TypeModifiers.") ? EnglishName : translated;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => DisplayName;
    }
}
