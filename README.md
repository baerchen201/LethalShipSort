## Usage

While in orbit, simply enter `/sort` in the game chat. This will sort all items.

By default, scrap is sorted into two piles of two- and one-handed items (with some exceptions, like the whoopie cushion, football, apparatus and beehive), tools are sorted into the cupboard.

If you don't have the cupboard on the ship, you can return it with the `cup` terminal command. Otherwise, all your items will just disappear.

By default, all tools on the cruiser are ignored, you can force include them by running `/sort -a`.

## Configuring custom positions

This mod can be configured to place any item anywhere, however this process may require some technical knowledge and a lot of patience.

You can change the default positions of one- and two-handed items, as well as tools, by changing the `default...` config values.
They use the same format as described [below](#for-vanilla-items)

### For vanilla items

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

### Using the `/put` command

For simple item placements you can use the `/put` command:

Syntax: `/put "<item>" { here | there } [ once | game | always ]`

 - `<item>` - The item name to move, for example `"Robot Toy"` or `ShotgunItem`
 - `{ here | there }` - `here` puts the item at the position where you're currently standing, `there` puts it where you're looking
 - `[ once | game | always ]` - `once` only moves the item once, `game` sets the item sort position for the current round and `always` saves the position in the config file (making it permanent)

Examples:
 - `/put clock there` - brings all clocks where you're looking
 - `/put walkietalkie here always` - sets the sorting position of walkie-talkies on your feet
 - `/put "rubber ducky" there game` - sets the sorting position of rubber duckies where you're looking, but only until you leave the game

### For modded/other items

The `customItemPositions` config value contains a list of other item positions by name.
This can be used to sort mod items, or items from future updates.

Format: `itemname:parent:x,y,z;itemname:parent:x,y,z`

### Raycasting

By default, all positions will trace a line downwards to find the closest spot on the ground (or any ship objects), and the items will be put there.
That way, no items will be floating mid-air.

However, if you prefer your items to be at the exact position you specified, you can disable this with the `UseRaycast` config value.

### Delayed sorting

If you like the visual effect of items flying to their positions one by one, you can set the `SortDelay` config value to add an interval between moving items.

Setting this to 250ms means, that only about 4 items will be sorted per second.
This makes the sorting process slower, but adds the satisfying visual effect.

## Autosorting

If you enable the AutoSort config value or use the chat command `/autosort` to toggle it,
all items will be automatically sorted when you leave a moon.

This only activates when you are the host