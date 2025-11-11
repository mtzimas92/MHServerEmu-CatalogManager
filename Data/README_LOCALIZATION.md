# Localization Guide

## Overview

The Catalog Manager includes a complete JSON-based localization system that allows users to translate the entire user interface into any language.

## Translation Files

Translation files are stored in: `Data/Localization/`

Each language should have has its own JSON file named with its language code:
- `en-US.json` - English (United States)
- `es-ES.json` - Spanish (Spain)
- `fr-FR.json` - French (France)
- etc.

## Creating a New Translation

### Step 1: Copy the English Template

1. Navigate to `Data/Localization/`
2. Copy `en-US.json` to a new file with your language code (e.g., `de-DE.json` for German)
3. Open the new file in a text editor

### Step 2: Update Language Metadata

At the top of the file, update the language information:

```json
{
  "Language": {
    "Code": "de-DE",
    "Name": "Deutsch (Deutschland)"
  },
  ...
}
```

### Step 3: Translate All Strings

Translate each string value (keep the keys in English):

**English:**
```json
"MainWindow": {
  "Title": "Marvel Heroes Catalog Manager",
  "Menu": {
    "File": "_File",
    "LoadCatalogFile": "_Load Catalog File..."
  }
}
```

**German:**
```json
"MainWindow": {
  "Title": "Marvel Heroes Katalog-Manager",
  "Menu": {
    "File": "_Datei",
    "LoadCatalogFile": "_Katalogdatei laden..."
  }
}
```

### Step 4: Handle Formatted Strings

Some strings may contain placeholders like `{0}`, `{1}`, etc. These will be replaced with dynamic values at runtime. Keep these placeholders in your translation:

**English:**
```json
"FileLoadedSuccessfully": "File loaded successfully: {0}"
```

**German:**
```json
"FileLoadedSuccessfully": "Datei erfolgreich geladen: {0}"
```

### Step 5: Preserve Keyboard Shortcuts

Menu items often have underscores (`_`) before a letter to indicate keyboard shortcuts. You can change which letter is underscored for your language:

**English:** `"_File"` (Alt+F)
**German:** `"_Datei"` (Alt+D)

## Translation File Structure

The translation file is organized into sections:

```json
{
  "Language": { ... },           // Language metadata
  "MainWindow": { ... },          // Main application window
  "AddItemWindow": { ... },       // Add Item dialog
  "SelectItemWindow": { ... },    // Item selection dialog
  "CreateBundleWindow": { ... },  // Bundle creation dialog
  "EditItemWindow": { ... },      // Edit Item dialog
  "BatchModifyWindow": { ... },   // Batch modification dialogs
  "BatchPriceUpdateWindow": { ... },
  "Common": { ... },              // Common terms (Yes, No, OK, etc.)
  "Categories": { ... },          // Item category names
  "TypeModifiers": { ... }        // Item type descriptions
}
```

### DO:
- ✓ Keep translation keys in English
- ✓ Preserve placeholders (`{0}`, `{1}`, etc.)
- ✓ Maintain JSON formatting (commas, quotes, brackets)
- ✓ Test with special characters from your language
- ✓ Keep keyboard shortcuts (`_` prefixes) but adapt the letter

### DON'T:
- ✗ Change translation key names (only translate values)
- ✗ Remove or reorder placeholders
- ✗ Break JSON syntax (use a JSON validator)
- ✗ Translate technical terms like SKU, GUID if they're universal
- ✗ Make translations excessively longer than the original (UI space is limited)

### Common Terms
```json
"Common": {
  "Yes": "Yes",      // French: "Oui", Spanish: "Sí", German: "Ja"
  "No": "No",        // French: "Non", Spanish: "No", German: "Nein"
  "OK": "OK",        // Usually kept as "OK" in most languages
  "Cancel": "Cancel" // French: "Annuler", Spanish: "Cancelar", German: "Abbrechen"
}
```

## Contributing Translations

If you create a translation for a new language, consider sharing it with the community:

1. Test your translation thoroughly
2. Ensure all strings are translated
3. Submit your translation file via GitHub or other sharing method
4. Include the language name and code in your submission

## Supported Languages

Currently available localizations:
- English (en-US) - Complete ✓

## Fallback Behavior

If a translation key is not found in the current language file, the application will:
1. Display the translation key itself (e.g., "MainWindow.Title")
2. Fall back to English if available
3. This helps identify missing translations during development

## Technical Details

- **Service:** `LocalizationService.cs` handles loading and retrieving translations
- **XAML Extension:** `TranslateExtension.cs` provides `{loc:Translate}` markup
- **Format:** JSON with UTF-8 encoding
- **Hot Reload:** Change language via the UI, at the bottom right corner, via a dropdown selector

## Troubleshooting

**Problem:** Translations not appearing
- **Solution:** Ensure language file exists in `Data/Localization/`
- **Solution:** Check JSON syntax with a validator

**Problem:** Some text is still in English
- **Solution:** Check if those UI elements use translation markup
- **Solution:** Add missing translation keys to your language file

**Problem:** Special characters appear as boxes/symbols
- **Solution:** Ensure file is saved as UTF-8 encoding
- **Solution:** Check font supports your language's character set

**Problem:** Application crashes on startup
- **Solution:** Validate JSON syntax (missing commas, brackets, quotes)
- **Solution:** Ensure Language.Code and Language.Name are present

