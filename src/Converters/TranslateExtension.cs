using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using CatalogManager.Services;

namespace CatalogManager.Converters
{
    /// <summary>
    /// XAML Markup Extension for localization with dynamic language switching support
    /// Usage: Text="{loc:Translate MainWindow.Menu.File}"
    /// </summary>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TranslateExtension()
        {
            Key = string.Empty;
        }

        public TranslateExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                return string.Empty;
            }

            // Create a binding to the LocalizationManager's indexer
            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationManager.Instance,
                Mode = BindingMode.OneWay
            };

            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                return binding.ProvideValue(serviceProvider);
            }

            return LocalizationService.Instance.GetString(Key);
        }
    }

    /// <summary>
    /// Singleton manager that notifies UI when language changes
    /// </summary>
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager? _instance;
        public static LocalizationManager Instance => _instance ??= new LocalizationManager();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationManager()
        {
            // Subscribe to language changes
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Notify that all translation keys have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        // Indexer for binding
        public string this[string key]
        {
            get => LocalizationService.Instance.GetString(key);
        }
    }
}
