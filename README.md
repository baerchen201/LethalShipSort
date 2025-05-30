## Usage

While in orbit, simply enter `/sort` in the game chat. This will sort all items.

Scrap is sorted into two piles of two- and one-handed items (with some exceptions, like the whoopie cushion, football, apparatus and beehive), tools are sorted into the cupboard.

If you don't have the cupboard on the ship, you can return it with the `cup` terminal command. Otherwise, all your items will just disappear. (TODO: check if cupboard on ship, put items in pile if not)

By default, all tools on the cruiser are ignored, you can force include them by running `/sort -a`.

## Configuring custom positions

This mod can be configured to place any item anywhere, however this process may require some technical knowledge and a lot of patience.

### For vanilla items (last updated v69)

For any item in the vanilla game, you can simply open the config file and edit the corresponding config value.

The value is formatted as follows:

`parent:x,y,z` or `x,y,z`

The parent is the object relative to which the position is interpreted as. 
If the parent is the storage closet, items will actually be put in the closet.

The parent object is specified as a path to the object based on the scene root \(for example `Environment/HangarShip/StorageCloset`, use [UnityExplorer](https://thunderstore.io/c/lethal-company/p/LethalCompanyModding/Yukieji_UnityExplorer/) to find this for any object\).

Alternatively, there are a couple keywords for common parent objects:
 - `closet` or `cupboard` for the storage closet \(`Environment/HangarShip/StorageCloset`\)
 - `file` or `filecabinet` for the file cabinet \(`Environment/HangarShip/FileCabinet`\)
 - `none` or `environment` for the world root \(`Environment`\)
 - `ship` for the ship \(`Environment/HangarShip`\), but you can also just not specify a parent since this is the default \(`ship:0,0,0`=`0,0,0`\)

### For modded/other items

The `customItemPositions` config value contains a list of other item positions by name.
This can be used to sort mod items, or items from future updates.

Format: `itemname:parent:0,0,0;itemname:parent:0,0,0`

## Autosorting

If you enable the AutoSort config value or use the chat command `/autosort` to toggle it,
all items will be automatically sorted when you leave a moon.

The lobby host may disable this if they don't want this mod to be used, or if they have it enabled themselves. // TODO: IMPLEMENT