using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Globalization;

namespace CatalogManager.Services
{
    /// <summary>
    /// Service for handling UI localization/translation
    /// </summary>
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        private Dictionary<string, object>? _translations;
        private string _currentLanguage = "en-US";
        private readonly string _localizationPath;

        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public string CurrentLanguage => _currentLanguage;
        
        /// <summary>
        /// Event fired when the language changes
        /// </summary>
        public event EventHandler? LanguageChanged;

        private LocalizationService()
        {
            _localizationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Localization");
            LoadLanguage("en-US");
        }

        /// <summary>
        /// Load a specific language file
        /// </summary>
        public bool LoadLanguage(string languageCode)
        {
            try
            {
                var languageFile = Path.Combine(_localizationPath, $"{languageCode}.json");
                
                if (!File.Exists(languageFile))
                {
                    // Try to load en-US as fallback
                    if (languageCode != "en-US")
                    {
                        languageFile = Path.Combine(_localizationPath, "en-US.json");
                        if (!File.Exists(languageFile))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                var json = File.ReadAllText(languageFile);
                _translations = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                _currentLanguage = languageCode;
                
                // Notify subscribers that language has changed
                LanguageChanged?.Invoke(this, EventArgs.Empty);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading language file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get a translated string by its key path (e.g., "MainWindow.Menu.File")
        /// </summary>
        public string GetString(string key, params object[] args)
        {
            if (_translations == null)
            {
                return key;
            }

            try
            {
                var parts = key.Split('.');
                object current = _translations;

                foreach (var part in parts)
                {
                    if (current is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue(part, out var value))
                        {
                            current = value;
                        }
                        else
                        {
                            return key; // Key not found, return the key itself
                        }
                    }
                    else if (current is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(part, out var property))
                        {
                            current = property;
                        }
                        else
                        {
                            return key;
                        }
                    }
                    else
                    {
                        return key;
                    }
                }

                // Convert the final value to string
                string result;
                if (current is JsonElement jsonElement)
                {
                    result = jsonElement.GetString() ?? key;
                }
                else
                {
                    result = current?.ToString() ?? key;
                }

                // Apply formatting if arguments provided
                if (args.Length > 0)
                {
                    try
                    {
                        result = string.Format(result, args);
                    }
                    catch
                    {
                        // If formatting fails, return unformatted string
                    }
                }

                return result;
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// Get available language codes
        /// </summary>
        public List<string> GetAvailableLanguages()
        {
            var languages = new List<string>();
            
            if (!Directory.Exists(_localizationPath))
            {
                return languages;
            }

            foreach (var file in Directory.GetFiles(_localizationPath, "*.json"))
            {
                var languageCode = Path.GetFileNameWithoutExtension(file);
                languages.Add(languageCode);
            }

            return languages;
        }

        /// <summary>
        /// Get language display name from language file
        /// </summary>
        public string GetLanguageName(string languageCode)
        {
            try
            {
                var languageFile = Path.Combine(_localizationPath, $"{languageCode}.json");
                if (!File.Exists(languageFile))
                {
                    return languageCode;
                }

                var json = File.ReadAllText(languageFile);
                var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("Language", out var langProp) &&
                    langProp.TryGetProperty("Name", out var nameProp))
                {
                    return nameProp.GetString() ?? languageCode;
                }
            }
            catch
            {
                // Ignore errors
            }

            return languageCode;
        }

        /// <summary>
        /// Shorthand for GetString
        /// </summary>
        public string this[string key] => GetString(key);
    }

    /// <summary>
    /// Extension class for easy access to localization
    /// </summary>
    public static class LocalizationExtensions
    {
        public static string T(this string key, params object[] args)
        {
            return LocalizationService.Instance.GetString(key, args);
        }
    }
}
