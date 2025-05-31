# Marvel Heroes Catalog Manager
## Description

A MHServerEmu-based tool that allows users to change the in-game store catalog easily.
It uses the game files to find common (and some uncommon or test) items that can be added to your store.  

MHServerEmu - CatalogManager allows you to edit and change almost everything in the in-game store. Restore stash tabs (Fantastic Four), add crazy test items, or even make your own bundles. 

## Setup Instructions

1. Download the latest release or download and compile the source code.
2. Extract the zip folder anywhere you want.
3. Copy Calligraphy.sip and mu_cdata.sip from Marvel Heroes\Data\Game to the Data/Game folder of the Catalog Manager. 

## Usage

1. Launch CatalogManager.exe
2. To add new items, click on the "Add new item" button on the top right. Click on Select Item on the new pop-up page and wait for the application to find all items that are supported.
3. Filter by category (Consumables, Character tokens, etc) and select your item. You can further filter by typing in the search bar. Once your item is found, click OK.
4. Now, add a short description, a Price, select the item type and the type modifier. (NOTE: Price must be minimum 1. In case of Stash Tab items, Always select the "StashPage" modifier, otherwise it will not work).
5. Click on Save and your item has now been added. Do this for any items you want to add.
6. You can also edit or remove existing catalog entries through the user interface. You can edit all entries in all catalogs but you can only delete entries in the "CatalogPatch" file. A warning is given.
7. You can batch modify, price update or delete items. Select the items you want and click on the corresponding Batch button. You can only batch modify items of similar type, and delete items of the "CatalogPatch" file. A warning is given. 
8. Find your modified files in the Data folder:
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
- Batch modify operations (for type modifiers). This is limited to items of the same type (i.e. costumes, bundles etc)
- Search by SKU, title, or prototype ID.

## Upcoming Features

- Bundles and BOGO items

## Disclaimer

This application is completely new. Any issues you may encounter, please report them so I can look into them. Before doing any work, make a backup for the Catalog.json and CatalogPatch.json files that are included in this. 
