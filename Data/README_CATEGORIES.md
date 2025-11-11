# Categories Configuration

## Overview
The `categories.json` file controls which item categories appear in the item selection dialog when creating new catalog entries or bundles.

## File Location
`Data/categories.json`

## Format
The file is a JSON array of category objects. Each category has three properties:

```json
{
  "DisplayName": "Name shown in UI",
  "Path": "Game database path to items",
  "IsInventoryType": true/false
}
```

### Properties

- **DisplayName** (string): The friendly name shown in the category dropdown
- **Path** (string): The prototype path in the game database
  - Can be a single path: `"Entity/Items/Consumables"`
  - Or multiple paths separated by `|`: `"Path1|Path2|Path3"`
- **IsInventoryType** (boolean):
  - `true` for inventory/stash items (uses `PlayerStashInventoryPrototype`)
  - `false` for regular items (uses `ItemPrototype`)

## Examples

### Single Path Category
```json
{
  "DisplayName": "Pets",
  "Path": "Entity/Items/Pets",
  "IsInventoryType": false
}
```

### Multiple Paths Category
```json
{
  "DisplayName": "Test Gear",
  "Path": "Entity/Items/Test|Entity/Items/Artifacts/Prototypes/Tier1Artifacts/RaidTest",
  "IsInventoryType": false
}
```

### Inventory Type Category
```json
{
  "DisplayName": "Stash Tabs",
  "Path": "Entity/Inventory/PlayerInventories/StashInventories/PageProtos/AvatarGear",
  "IsInventoryType": true
}
```

## Adding New Categories

1. Open `Data/categories.json` in a text editor
2. Add a new entry to the array:
```json
{
  "DisplayName": "Your Category Name",
  "Path": "Entity/Path/To/Items",
  "IsInventoryType": false
}
```
3. Save the file
4. Restart the application

## Common Item Paths

Here are some common item paths you might want to add:

- **Artifacts**: `Entity/Items/Artifacts`
- **Legendary Items**: `Entity/Items/LegendaryItems`
- **Medals**: `Entity/Items/Medals`
- **Relics**: `Entity/Items/Relics`
- **Rings**: `Entity/Items/Rings`
- **Team-Up Gear**: `Entity/Items/TeamUpGear`
- **Insignias**: `Entity/Items/Insignias`

## Design State Filtering

The application automatically filters items by design state:
- Only shows items with `DesignState = Live` or `DevelopmentOnly`

## Notes

- If `categories.json` is missing, the application will create it with default categories
- The pipe separator (`|`) allows combining multiple paths into one category
- Category order in the file determines the order in the UI dropdown
- Invalid JSON will cause the application to fall back to default categories
