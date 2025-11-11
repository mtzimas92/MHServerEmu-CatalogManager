# Marvel Heroes Catalog Manager

## Description

A MHServerEmu-based tool that allows users to change the in-game store catalog easily. It uses the game files to find common (and some uncommon or test) items that can be added to your store.

MHServerEmu CatalogManager allows you to edit and change almost everything in the in-game store. Restore stash tabs (Fantastic Four), add crazy test items, or even make your own bundles.

## Setup Instructions

1. Download the latest release or download and compile the source code.
2. Extract the zip folder anywhere you want.
3. Copy `Calligraphy.sip` and `mu_cdata.sip` from `Marvel Heroes\Data\Game` to the `Data/Game` folder of the Catalog Manager.

## Usage

### Loading Catalog Files

1. Launch `CatalogManager.exe`
2. Load your catalog files via **File → Load Catalog File**
   - You can load multiple files (e.g., `CatalogBoost.json`, `CatalogHeroes.json`, `CatalogBundles.json`)
   - All loaded files merge together in a unified view
   - Use **File → Clear All Files** to reset and start fresh
3. View currently loaded files by hovering over **File → Current Files**

### Adding New Items

1. Click **Add New Item** button
2. In the item selection dialog, choose a category (Consumables, Costumes, etc.)
3. Wait for the application to load all available items
4. Use the search bar to filter items by name or path
5. Select your item and click **OK**
6. Fill in the details:
   - **Title**: Display name for the item
   - **Description**: Short description
   - **Price**: Minimum 1 G (in-game currency)
   - **Item Type**: Category (Hero, Costume, Boost, etc.)
   - **Type Modifiers**: Special flags (e.g., "StashPage" for stash tabs - **required**)
7. Click **Save** - the item will be added to `{SourceFile}_MODIFIED.json`

### Editing & Deleting Items

- **Edit**: Select an item and click **Edit** to modify its properties
- **Delete**: 
  - Check **"Enable Stock Catalog Deletion"** to enable delete buttons
  - Warning: This deletes items from their source files (with backup)
  - Items are removed from the original catalog file, not just hidden

### Batch Operations

- **Batch Modify**: Update type modifiers for multiple items (must be same type)
- **Batch Price Update**: Change prices for multiple selected items
- **Batch Delete**: Remove multiple items at once

### Modified Files Workflow

All changes save to `{filename}_MODIFIED.json` files:
- `CatalogBoost.json` → `CatalogBoost_MODIFIED.json`
- `CatalogHeroes.json` → `CatalogHeroes_MODIFIED.json`
- etc.

**Benefits:**
- Original catalog files remain untouched
- Modified files automatically load alongside their base files
- Each category has independent modification tracking
- Deploy only the `_MODIFIED` files you need

### Finding Your Modified Files

Modified files are saved in the same directory as their source files. Typically:
- `Data/CatalogBoost_MODIFIED.json`
- `Data/CatalogHeroes_MODIFIED.json`
- `Data/CatalogBundles_MODIFIED.json`
- etc.

## Deployment

Copy the `_MODIFIED.json` files to your MHServerEmu installation:

**Option 1 - Use Modified Files (Recommended):**
```
MHServerEmu/Data/Game/MTXStore/CatalogBoost_MODIFIED.json
MHServerEmu/Data/Game/MTXStore/CatalogHeroes_MODIFIED.json
MHServerEmu/Data/Game/MTXStore/CatalogBundles_MODIFIED.json
```

**Option 2 - Replace Base Files:**
- Merge modified entries into base catalog files
- Or replace base files entirely (keep backups!)

## Features

### Catalog Management
- **Multi-file Support**: Load any number of catalog files simultaneously
- **Flexible Organization**: Separate catalogs by category (Boosts, Heroes, Costumes, etc.)
- **Non-Destructive Edits**: Original files never modified
- **Smart SKU Management**: Prevents duplicate SKU IDs across all files
- **Browse & Search**: Search by SKU, title, or prototype ID
- **Category Filtering**: Filter items by type
- **Price Range Filtering**: Find items within specific price ranges

### Batch Operations
- **Batch Modify**: Update type modifiers for multiple items
- **Batch Price Update**: Change prices in bulk
- **Batch Delete**: Remove multiple items at once

### Bundles & BOGO
- **Bundle Creator**: Build custom bundles with multiple items
- **HTML Generator**: Auto-generates bundle description pages
- **Thumbnail Creator**: Creates bundle preview images
- **Hardcoded URLs**: Uses Marvel Heroes CDN paths for consistency

### Customization
- **Categories Config**: Edit `Data/categories.json` to add custom item categories
- **Display Names**: Use OpenCalligraphy to discover item paths
- **Type Modifiers**: Predefined for all categories, discoverable from loaded catalogs

## Bundle Items

Bundle items include auto-generated description pages and thumbnails. When you create a bundle:

**Generated Files** (in `WebContent` folder):
```
WebContent/
├── css/
│   └── bundle.css
├── html/
│   └── {BundleName}_en_bundle.html
└── images/
    └── MTX_Store_Bundle_{BundleName}_Thumb.png
```

**URLs** (hardcoded to Marvel Heroes CDN):
- HTML: `http://storecdn.marvelheroes.com/cdn/en_us/bundles/{title}_en_bundle.html`
- Image: `http://storecdn.marvelheroes.com/bundles/MTX_Store_Bundle_{title}_Thumb.png`

**Deployment:**
1. Copy files to your Apache server:
   - HTML → `Apache24/htdocs/bundles/`
   - CSS → `Apache24/htdocs/bundles/css/`
   - Images → `Apache24/htdocs/bundles/images/`
2. Or host them on your own CDN matching the hardcoded paths

## Configuration

### Categories
Edit `Data/categories.json` to customize item categories. See `Data/README_CATEGORIES.md` for details.

Use **OpenCalligraphy** to discover item paths: https://github.com/Crypto137/OpenCalligraphy

## Disclaimer

**Important Notes:**
- Always backup your catalog files before making changes
- Some items may require specific type modifiers to work correctly
- Stash tabs **must** have the "StashPage" modifier

**Reporting Issues:**
Please report any bugs or issues you encounter. This helps improve the tool for everyone!

## Credits

- Built for **MHServerEmu**: https://github.com/Crypto137/MHServerEmu
- Uses **OpenCalligraphy** for item discovery: https://github.com/Crypto137/OpenCalligraphy
