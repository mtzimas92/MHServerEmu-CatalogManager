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
2. Add your preferred catalog files via the file -> load catalog files option (you can clear the loaded items by selecting the clear option).
3. To add new items, click on the "Add new item" button on the top right. Click on Select Item on the new pop-up page and wait for the application to find all items that are supported.
4. Filter by category (Consumables, Character tokens, etc) and select your item. You can further filter by typing in the search bar. Once your item is found, click OK.
5. Now, add a short description, a Price, select the item type and the type modifier. (NOTE: Price must be minimum 1. In case of Stash Tab items, Always select the "StashPage" modifier, otherwise it will not work).
6. Click on Save and your item has now been added to a _MODIFIED.json file depending on your initial category. Do this for any items you want to add.
7. You can also edit or remove existing catalog entries through the user interface. You can edit all entries in all catalogs and they will be added in the _MODIFIED.json file. 
8. You can batch modify, price update items. Select the items you want and click on the corresponding Batch button. You can only batch modify items of similar type. 
9. Find your modified files in the Data folder:
   - CatalogBoost_MODIFIED.json
   - CatalogHeroes_MODIFIED.json
   - CatalogBundles_MODIFIED.json (etc.)

## Deployment

Copy the modified json files to your MHServerEmu installation:
- MHServerEmu/Data/Game/MTXStore/CatalogBoost_MODIFIED.json
- MHServerEmu/Data/Game/MTXStore/CatalogHeroes_MODIFIED.json (etc.)

## Features

- Browse and search through all game items that are currently in the store.
- Filter items by category.
- Add new items to the catalog
- Bundles and BOGO items.
- Edit existing items.
- Set item prices and type modifiers.
- Price range filtering.
- Batch operations.
- Search by SKU, title, or prototype ID.

## Bundle Items
- Bundle items normally require a description page and a thumbnail so you can see the full contents. From version 0.4 and forward, there is an HTML generator and a thumbnail creator which has a very basic functionality to create the description page and the thumbnail. The result per bundle is stored in a new folder (WebContent), which contains:
   - WebContent
      - css
      - html
      - images
- Once a bundle is created, you can copy the files from the CSS, HTML, and images folders and put them in Apache24/htdocs/bundles. The images and css should be placed in an "images" and "css" folder respectively, inside the "bundles" folder. 
## Disclaimer

This application is completely new. Any issues you may encounter, please report them so I can look into them. Before doing any work, make a backup for the Catalog.json and CatalogPatch.json files that are included in this. 

