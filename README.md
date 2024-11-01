# Marvel Heroes Catalog Manager

## Setup Instructions

1. Download the application binaries
2. Place your game files in the Data/Game folder:
   - Calligraphy.sip
   - mu_cdata.sip

## Usage

1. Launch CatalogManager.exe
2. Add, edit, or remove catalog entries through the user interface. You can edit all entries in all catalogs but you can only delete entries in the "CatalogPatch" file.
3. Find your modified files in the Data folder:
   - Catalog.json
   - CatalogPatch.json

## Deployment

Copy the modified json files to your MHServerEmu installation:
- MHServerEmu/Data/Billing/Catalog.json
- MHServerEmu/Data/Billing/CatalogPatch.json

## Features

- Browse and search through all game items
- Filter items by category
- Add new items to the catalog
- Edit existing items
- Set item prices and type modifiers
- Price range filtering
- Batch price updates
- Batch delete operations
- Search by SKU, title, or prototype ID

The catalog manager will now be ready to use with your Marvel Heroes Emulator server.
