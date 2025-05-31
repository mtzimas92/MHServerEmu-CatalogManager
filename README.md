# Marvel Heroes Catalog Manager
## Description

A MHServerEmu-based tool that allows users to change the in-game store catalog easily.
It uses the game files to find common (and some uncommon or test) items that can be added to your store.  
Using this application, you can bring back the stash tabs for F4 heroes which were removed from MHO in later iterations, in an easy-to-use UI.
You can change the current catalog prices, you can create your own bundles or BOGO offers, or enable items that you cannot currently see but exist in the current catalog. 

## Setup Instructions

1. Download the application binaries (Found under Releases/CatalogManager-v0.2.zip). Alternatively, download the source code and compile yourself.
2. Extract the zip folder.
3. Place your game files in the Data/Game folder:
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

- Browse and search through all game items that are currently in the store.
- Filter items by category.
- Add new items to the catalog
- Edit existing items.
- Set item prices and type modifiers.
- Price range filtering.
- Batch price updates.
- Batch delete operations.
- Search by SKU, title, or prototype ID.
  
## Disclaimer

This application is completely new. Any issues you may encounter, please report them so I can look into them. Before doing any work, make a backup for the Catalog.json and CatalogPatch.json files that are included in this. 
