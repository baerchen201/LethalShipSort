# ShipSort - Item sorting mod for Lethal Company

## Documentation for users

### Basic usage

After installing the mod, simply type `/sort` in the game chat.

This should sort all scrap on your ship into two piles (One- and Two-handed items) and put all your tools into the cupboard (or on the floor, if the cupboard is stored)

For maximum consistency you can only use `/sort` while your ship is in orbit.

### Advanced usage (alternative/custom scripts)

This mod is highly customizable by using [lua](https://www.lua.org/start.html) scripts to create item arrangements.

The default arrangement described above is produced by the bundled `sort.lua` script, however it can be easily adjusted by changing the `ScriptPath` config value.

**While scripts run in an isolated environment, there is always a risk when running arbitrary code. Only use scripts from trusted sources**

The path is interpreted relative to the BepInEx/config directory in your profile or as an absolute path.

**An alternative script is bundled with the mod: `sort_sm.lua` - a reimplementation of the [ScrapMagic](https://thunderstore.io/c/lethal-company/p/KyleTheScientist/Scrap_Magic/) sorting layout**

### Other config values

- `General/Timeout`: To prevent freezes caused by bad scripts, a maximum execution time limit is set. If this expires, the script execution will be aborted and no items will be sorted.
- `Networking/ShareConfig`: The script you selected will be shared with any clients also using the mod in your lobby. This results in less config conflicts.
- `Networking/UseSharedConfig`: If you joined a lobby where the host has shared their script, this can be used to force your own script instead.
- `Networking/SharedConfigSizeLimit`: Prevents any abnormally large scripts sent by the host from being decompressed.

### Command arguments

<sub>Command arguments are anything you type after the command: `/command argument1 argument2 ...`</sub>

Any arguments you pass to the `/sort` command are passed to the script, except for `reload`, which reloads the currently selected script from disk.

### Common errors

- `Script '...' could not be found`: The script file referenced in the config is not available. Make sure the path is valid.
- `Script execution timed out`: The script took too long to execute, increase the `General/Timeout` config value if this happens too often.
- `Version conflict (...)`: The script was created for a different version of the mod, things may break.
If it works fine, you can remove the `expect_version(...)` instruction in the script to remove the warning.
- `... items couldn't be sorted`: Some positions were invalid, contact the creator about this and make sure to provide all error messages found in the game logs.
- `Script error: ...`: The script failed to execute, contact the creator about this and make sure to provide the full error message found in the game logs.
- `Script compilation error: ...`: The script file contains an error, contact the creator about this and make sure to provide the full error message found in the game logs.
- `Script result invalid: ...`: The script exited abnormally, contact the creator about this and make sure to provide the full error message found in the game logs.

## Documentation for script developers

<sub>When developing scripts, it is recommended to use a debug build of the runtime, as it provides useful tools, warnings and log messages for script development. See [below](#debug-build) for more information</sub>

### Concept

A sorting script receives a [table of items](#globals) and returns a [table of positions](#return-values), both using arbitrary indexes for item identification.

The script should use its code to generate those item positions.

#### Return values

The script should return a table containing [ItemPos](#itempos) or [Vector3](#vector3) objects at the indices of their respective items.

If an item has no assigned position (`nil`), the item will not be moved.

### Lua environment

This mod uses [Lua-CSharp](https://github.com/nuskey8/Lua-CSharp/tree/v0.5.6#compatibility) (v0.5.6 for Lua 5.2) to run scripts, see its compatibility section for more information.

Only the [basic](https://www.lua.org/manual/5.2/manual.html#6.1), [string](https://www.lua.org/manual/5.2/manual.html#6.4), [table](https://www.lua.org/manual/5.2/manual.html#6.5), [math](https://www.lua.org/manual/5.2/manual.html#6.6) and [bitwise](https://www.lua.org/manual/5.2/manual.html#6.7) libraries are loaded.

The `loadfile` and `dofile` functions are removed.

### Globals

- `items` (table): A list of [item definitions](#item-definitions)
- `moon` (table): A table containing information about the current moon (see [moon definition](#moon-definitions))
- `remaining_days` (int): The amount of days remaining in the current quota
- `unlockables` (table): A table of booleans representing different unlockables on the ship and their availability (use the [Unlockables enum](#unlockables) to index this)
- `args` (table): A list containing the user-provided command arguments
- `about` (string): A short description of the current runtime (`baer1.ShipSort vX.X.X`)
- `script` (string): The file name of the current script (not the full path)
- `version_major` (int): The major version of the runtime
- `version_minor` (int): The minor version of the runtime
- `version_patch` (int): The patch version of the runtime

#### Item definitions

Each item definition is a table with the following values:

- `name` (string): The item object name, without the `(Clone)` added by Unity (Shovel: `ShovelItem`, Bottles: `BinFullOfBottles`, see [below](#vanilla-items))
- `type` (string): The item script type (Zap Gun: `PatcherTool`, _Generic scrap_: `PhysicsProp`, etc.)
- `scrap` (bool): Whether the item is scrap (not a tool)
- `large` (bool): Whether the item is two-handed
- `arg` (object): Misc. value related to the item (amount of shells in a shotgun, whether a radar booster is enabled, see `SortAPI.ItemArg` for more details)
- `index` (int): 1-based index of the item based on `name`
- `count` (int): total amount of items with matching `name`

#### Vanilla Items

_Last updated for v81_

| Name                     | Type                     | Scrap | Large | Argument |
|--------------------------|--------------------------|-------|-------|----------|
| `Airhorn`                | `NoisemakerProp`         | True  | False | nil      |
| `BBFlashlight`           | `FlashlightItem`         | False | False | nil      |
| `BeltBagItem`            | `BeltBagItem`            | False | False | number   |
| `BigBolt`                | `PhysicsProp`            | True  | False | nil      |
| `BinFullOfBottles`       | `PhysicsProp`            | True  | True  | nil      |
| `Binoculars`             | `BinocularsItem`         | False | False | nil      |
| `Bone`                   | `PhysicsProp`            | True  | False | nil      |
| `Boombox`                | `BoomboxItem`            | False | False | nil      |
| `Candy`                  | `PhysicsProp`            | True  | False | nil      |
| `CashRegisterItem`       | `NoisemakerProp`         | True  | True  | nil      |
| `CaveDwellerEnemy`       | `CaveDwellerPhysicsProp` | False | True  | nil      |
| `ChemicalJug`            | `PhysicsProp`            | True  | True  | nil      |
| `ClipboardManual`        | `ClipboardItem`          | False | False | nil      |
| `Clock`                  | `ClockProp`              | True  | False | nil      |
| `Clownhorn`              | `NoisemakerProp`         | True  | False | nil      |
| `Cog`                    | `PhysicsProp`            | True  | True  | nil      |
| `ComedyMask`             | `HauntedMaskItem`        | True  | False | nil      |
| `CompanyCruiserManual`   | `ClipboardItem`          | False | False | nil      |
| `ControlPad`             | `PhysicsProp`            | True  | True  | nil      |
| `CookieMoldPan`          | `PhysicsProp`            | True  | False | nil      |
| `Dentures`               | `AnimatedItem`           | True  | False | nil      |
| `DiyFlashbang`           | `StunGrenadeItem`        | True  | False | boolean  |
| `Dustpan`                | `PhysicsProp`            | True  | False | nil      |
| `Ear`                    | `RandomFlyParticle`      | True  | False | nil      |
| `EasterEgg`              | `StunGrenadeItem`        | True  | False | boolean  |
| `EggBeater`              | `PhysicsProp`            | True  | False | nil      |
| `EnginePart`             | `PhysicsProp`            | True  | True  | nil      |
| `ExtensionLadderItem`    | `ExtensionLadderItem`    | False | False | nil      |
| `FancyGlass`             | `PhysicsProp`            | True  | False | nil      |
| `FancyLamp`              | `PhysicsProp`            | True  | True  | nil      |
| `FancyRing`              | `PhysicsProp`            | True  | False | nil      |
| `FishTestProp`           | `PhysicsProp`            | True  | False | nil      |
| `FlashlightItem`         | `FlashlightItem`         | False | False | nil      |
| `Flask`                  | `PhysicsProp`            | True  | False | nil      |
| `GarbageLid`             | `PhysicsProp`            | True  | True  | nil      |
| `GiftBox`                | `GiftBoxItem`            | True  | False | nil      |
| `GoldBar`                | `PhysicsProp`            | True  | False | nil      |
| `Hairbrush`              | `PhysicsProp`            | True  | False | nil      |
| `Hairdryer`              | `NoisemakerProp`         | True  | False | nil      |
| `HandBell`               | `EventWhenDroppedItem`   | True  | False | nil      |
| `HeartContainer`         | `PhysicsProp`            | True  | True  | nil      |
| `JetpackItem`            | `JetpackItem`            | False | False | boolean  |
| `Key`                    | `KeyItem`                | False | False | nil      |
| `KiwiBabyItem`           | `KiwiBabyItem`           | True  | True  | nil      |
| `KnifeItem`              | `KnifeItem`              | True  | False | nil      |
| `LaserPointer`           | `FlashlightItem`         | True  | False | nil      |
| `LockPickerItem`         | `LockPicker`             | False | False | nil      |
| `LungApparatus`          | `LungProp`               | True  | True  | nil      |
| `LungApparatusTurnedOff` | `LungProp`               | True  | True  | nil      |
| `Magic7Ball`             | `PhysicsProp`            | True  | False | nil      |
| `MagnifyingGlass`        | `PhysicsProp`            | True  | False | nil      |
| `MappingDevice`          | `MapDevice`              | False | False | nil      |
| `MetalSheet`             | `PhysicsProp`            | True  | False | nil      |
| `Mug`                    | `PhysicsProp`            | True  | False | nil      |
| `OldPhone`               | `AnimatedItem`           | True  | False | nil      |
| `Painting`               | `PhysicsProp`            | True  | True  | nil      |
| `PatcherGunItem`         | `PatcherTool`            | False | False | nil      |
| `PerfumeBottle`          | `PhysicsProp`            | True  | False | nil      |
| `PickleJar`              | `PhysicsProp`            | True  | False | nil      |
| `PillBottle`             | `PhysicsProp`            | True  | False | nil      |
| `PlasticCup`             | `PhysicsProp`            | True  | False | nil      |
| `RadarBoosterDevice`     | `RadarBoosterItem`       | False | False | boolean  |
| `RagdollGrabbableObject` | `RagdollGrabbableObject` | True  | True  | nil      |
| `RedLocustHive`          | `PhysicsProp`            | True  | True  | nil      |
| `RedSodaCan`             | `PhysicsProp`            | True  | False | nil      |
| `Remote`                 | `RemoteProp`             | True  | False | nil      |
| `RibcageBone`            | `PhysicsProp`            | True  | True  | nil      |
| `RobotToy`               | `AnimatedItem`           | True  | False | nil      |
| `RubberDucky`            | `AnimatedItem`           | True  | False | nil      |
| `SeveredFootLOD0`        | `RandomFlyParticle`      | True  | False | nil      |
| `SeveredHandLOD0`        | `RandomFlyParticle`      | True  | False | nil      |
| `SeveredThighLOD0`       | `RandomFlyParticle`      | True  | False | nil      |
| `ShotgunItem`            | `ShotgunItem`            | True  | False | number   |
| `ShotgunShell`           | `GunAmmo`                | False | False | nil      |
| `ShovelItem`             | `Shovel`                 | False | False | nil      |
| `SoccerBall`             | `SoccerBallProp`         | True  | True  | nil      |
| `SprayPaintItem`         | `SprayPaintItem`         | False | False | number   |
| `SteeringWheel`          | `PhysicsProp`            | True  | False | nil      |
| `StickyNoteItem`         | `PhysicsProp`            | False | False | nil      |
| `StopSign`               | `Shovel`                 | True  | False | nil      |
| `StunGrenade`            | `StunGrenadeItem`        | False | False | boolean  |
| `TeaKettle`              | `PhysicsProp`            | True  | False | nil      |
| `ToiletPaperRolls`       | `PhysicsProp`            | True  | True  | nil      |
| `Tongue`                 | `AnimatedItem`           | True  | False | nil      |
| `Toothpaste`             | `PhysicsProp`            | True  | False | nil      |
| `ToyCube`                | `PhysicsProp`            | True  | False | nil      |
| `ToyTrain`               | `AnimatedItem`           | True  | False | nil      |
| `TragedyMask`            | `HauntedMaskItem`        | True  | False | nil      |
| `TZPChemical`            | `TetraChemicalItem`      | False | False | number   |
| `WalkieTalkie`           | `WalkieTalkie`           | False | False | nil      |
| `WeedKillerItem`         | `SprayPaintItem`         | False | False | number   |
| `WhoopieCushion`         | `WhoopieCushionItem`     | True  | False | nil      |
| `YieldSign`              | `Shovel`                 | True  | False | nil      |
| `ZeddogPlushie`          | `PhysicsProp`            | True  | False | nil      |

#### Moon definitions

A moon definition is a table with the following values:

- `id` (int): The internal moon id (`SelectableLevel.levelID`, see [below](#vanilla-moons))
- `name` (string): The moon name
- `scene` (string): The moon terrain scene

#### Vanilla Moons

_Last updated for v81_

| ID | Name                 | Scene                   |
|----|----------------------|-------------------------|
| -1 | _none_               | `IntroScene2`           |
| 0  | `41 Experimentation` | `Level1Experimentation` |
| 1  | `220 Assurance`      | `Level2Assurance`       |
| 2  | `56 Vow`             | `Level3Vow`             |
| 3  | `71 Gordion`         | `CompanyBuilding`       |
| 4  | `61 March`           | `Level4March`           |
| 5  | `20 Adamance`        | `Level10Adamance`       |
| 6  | `85 Rend`            | `Level5Rend`            |
| 7  | `7 Dine`             | `Level6Dine`            |
| 8  | `21 Offense`         | `Level7Offense`         |
| 9  | `8 Titan`            | `Level8Titan`           |
| 10 | `68 Artifice`        | `Level9Artifice`        |
| 11 | `44 Liquidation`     | `Level12Liquidation`    |
| 12 | `5 Embrion`          | `Level11Embrion`        |

### Enums

Some enums are made available as global constants (Enum.Value would become ENUM_VALUE)

#### Unlockables

| Name                            | Value |
|---------------------------------|-------|
| `UNLOCKABLE_CRUISER`            | 0     |
| `UNLOCKABLE_ORANGE_SUIT`        | 1     |
| `UNLOCKABLE_GREEN_SUIT`         | 2     |
| `UNLOCKABLE_HAZARD_SUIT`        | 3     |
| `UNLOCKABLE_PAJAMA_SUIT`        | 4     |
| `UNLOCKABLE_COZY_LIGHTS`        | 5     |
| `UNLOCKABLE_TELEPORTER`         | 6     |
| `UNLOCKABLE_TELEVISION`         | 7     |
| `UNLOCKABLE_CUPBOARD`           | 8     |
| `UNLOCKABLE_FILE_CABINET`       | 9     |
| `UNLOCKABLE_TOILET`             | 10    |
| `UNLOCKABLE_SHOWER`             | 11    |
| `UNLOCKABLE_LIGHTS`             | 12    |
| `UNLOCKABLE_RECORD_PLAYER`      | 13    |
| `UNLOCKABLE_TABLE`              | 14    |
| `UNLOCKABLE_ROMANTIC_TABLE`     | 15    |
| `UNLOCKABLE_BUNKBEDS`           | 16    |
| `UNLOCKABLE_SIGNAL_TRANSLATOR`  | 18    |
| `UNLOCKABLE_LOUD_HORN`          | 19    |
| `UNLOCKABLE_INVERSE_TELEPORTER` | 20    |
| `UNLOCKABLE_JACK_O_LANTERN`     | 21    |
| `UNLOCKABLE_WELCOME_MAT`        | 22    |
| `UNLOCKABLE_GOLDFISH`           | 23    |
| `UNLOCKABLE_PLUSHIE_PAJAMA_MAN` | 24    |
| `UNLOCKABLE_PURPLE_SUIT`        | 25    |
| `UNLOCKABLE_BEE_SUIT`           | 26    |
| `UNLOCKABLE_BUNNY_SUIT`         | 27    |
| `UNLOCKABLE_DISCO_BALL`         | 28    |
| `UNLOCKABLE_MICROWAVE`          | 29    |
| `UNLOCKABLE_SOFA_CHAIR`         | 30    |
| `UNLOCKABLE_FRIDGE`             | 31    |
| `UNLOCKABLE_CLASSIC_PAINTING`   | 32    |
| `UNLOCKABLE_ELECTRIC_CHAIR`     | 33    |
| `UNLOCKABLE_DOG_HOUSE`          | 34    |

#### Parent objects

| Name               | Value |
|--------------------|-------|
| `PARENT_CRUISER`   | -1    |
| `PARENT_SHIP`      | 0     |
| `PARENT_CUPBOARD`  | 7     |
| `PARENT_MICROWAVE` | 28    |
| `PARENT_FRIDGE`    | 30    |

#### Relative objects

| Name                          | Value |
|-------------------------------|-------|
| `RELATIVE_PARENT`             | 0     |
| `RELATIVE_WORLD`              | 1     |
| `RELATIVE_TELEPORTER`         | 5     |
| `RELATIVE_TELEVISION`         | 6     |
| `RELATIVE_FILE_CABINET`       | 8     |
| `RELATIVE_TOILET`             | 9     |
| `RELATIVE_SHOWER`             | 10    |
| `RELATIVE_RECORD_PLAYER`      | 12    |
| `RELATIVE_TABLE`              | 13    |
| `RELATIVE_ROMANTIC_TABLE`     | 14    |
| `RELATIVE_BUNKBEDS`           | 15    |
| `RELATIVE_TERMINAL`           | 16    |
| `RELATIVE_SIGNAL_TRANSLATOR`  | 17    |
| `RELATIVE_LOUD_HORN`          | 18    |
| `RELATIVE_INVERSE_TELEPORTER` | 19    |
| `RELATIVE_JACK_O_LANTERN`     | 20    |
| `RELATIVE_WELCOME_MAT`        | 21    |
| `RELATIVE_GOLDFISH`           | 22    |
| `RELATIVE_PLUSHIE_PAJAMA_MAN` | 23    |
| `RELATIVE_DISCO_BALL`         | 27    |
| `RELATIVE_SOFA_CHAIR`         | 29    |
| `RELATIVE_CLASSIC_PAINTING`   | 31    |
| `RELATIVE_ELECTRIC_CHAIR`     | 32    |
| `RELATIVE_DOG_HOUSE`          | 33    |

#### Rotation modes

| Name            | Value |
|-----------------|-------|
| `ROTATE_LOCAL`  | 0     |
| `ROTATE_PARENT` | 1     |
| `ROTATE_WORLD`  | 2     |
| `ROTATE_NONE`   | 3     |

#### Transform objects

| Name                           | Value |
|--------------------------------|-------|
| `TRANSFORM_CRUISER`            | -1    |
| `TRANSFORM_SHIP`               | 0     |
| `TRANSFORM_WORLD`              | 1     |
| `TRANSFORM_TELEPORTER`         | 5     |
| `TRANSFORM_TELEVISION`         | 6     |
| `TRANSFORM_CUPBOARD`           | 7     |
| `TRANSFORM_FILE_CABINET`       | 8     |
| `TRANSFORM_TOILET`             | 9     |
| `TRANSFORM_SHOWER`             | 10    |
| `TRANSFORM_RECORD_PLAYER`      | 12    |
| `TRANSFORM_TABLE`              | 13    |
| `TRANSFORM_ROMANTIC_TABLE`     | 14    |
| `TRANSFORM_BUNKBEDS`           | 15    |
| `TRANSFORM_TERMINAL`           | 16    |
| `TRANSFORM_SIGNAL_TRANSLATOR`  | 17    |
| `TRANSFORM_LOUD_HORN`          | 18    |
| `TRANSFORM_INVERSE_TELEPORTER` | 19    |
| `TRANSFORM_JACK_O_LANTERN`     | 20    |
| `TRANSFORM_WELCOME_MAT`        | 21    |
| `TRANSFORM_GOLDFISH`           | 22    |
| `TRANSFORM_PLUSHIE_PAJAMA_MAN` | 23    |
| `TRANSFORM_DISCO_BALL`         | 27    |
| `TRANSFORM_MICROWAVE`          | 28    |
| `TRANSFORM_SOFA_CHAIR`         | 29    |
| `TRANSFORM_FRIDGE`             | 30    |
| `TRANSFORM_CLASSIC_PAINTING`   | 31    |
| `TRANSFORM_ELECTRIC_CHAIR`     | 32    |
| `TRANSFORM_DOG_HOUSE`          | 33    |

### Types/Classes

#### Vector3

Each Vector3 contains `x`, `y` and `z` values. You can create one using `Vector3(float x, float y, float z)`

The following operations are supported:
- `Vector3:x`
- `Vector3:y`
- `Vector3:z`
- `Vector3:Equals(Vector3)`
- `Vector3:normalized()`
- `Vector3 + Vector3`
- `Vector3 - Vector3`
- `Vector3 * float`
- `Vector3 / float`
- `-Vector3`
- `Vector3[0-2]` (zero-indexed)

Some commonly used values have constant representations:

| Name              | Value               |
|-------------------|---------------------|
| `VECTOR3_ZERO`    | `Vector3(0, 0, 0)`  |
| `VECTOR3_ONE`     | `Vector3(1, 1, 1)`  |
| `VECTOR3_DOWN`    | `Vector3(0, -1, 0)` |
| `VECTOR3_UP`      | `Vector3(0, 1, 0)`  |
| `VECTOR3_LEFT`    | `Vector3(-1, 0, 0)` |
| `VECTOR3_RIGHT`   | `Vector3(1, 0, 0)`  |
| `VECTOR3_FORWARD` | `Vector3(0, 0, 1)`  |
| `VECTOR3_BACK`    | `Vector3(0, 0, -1)` |

#### ItemPos

An `ItemPos` object represents instructions on how to position a certain item, such as a position and rotation.

It is made up of the following fields:

- `position` ([Vector3](#vector3)): The coordinates where to put the item
- `parent_to` ([PARENT](#parent-objects)) [DEFAULT: PARENT_SHIP]: The transform to which to parent the item
- `relative_to` ([RELATIVE](#relative-objects)) [DEFAULT: RELATIVE_PARENT]: The transform relative to which the position is calculated
- `rotation` (int) [DEFAULT: 0]: The rotation of the item around the Y axis (always limited to 0-360)
- `rotation_mode` ([ROTATE](#rotation-modes)) [DEFAULT: ROTATE_LOCAL]: How to adjust the rotation angle

An `ItemPos` is created using `ItemPos(Vector3 position)` and additional fields can be set using `:with_field(value)`:

`ItemPos(VECTOR3_ZERO):with_rotation(90):with_rotation_mode(ROTATE_WORLD)`

#### RaycastPos

A `RaycastPos` object represents instructions on how to perform a raycast.

It is made up of the following fields:

- `position` ([Vector3](#vector3)): The raycast origin
- `direction` ([Vector3](#vector3)): The direction to cast the raycast in
- `relative_to` ([TRANSFORM](#transform-objects)): The transform relative to which the position and direction are calculated

While a `RaycastPos` can be created manually using `RaycastPos(Vector3 position, Vector3 direction, int relative_to)`, unless you are using all three options you should just use the [`raycast` function](#raycast) directly.

### Utility functions

#### Print

`void print(...)`

The `print` function outputs the provided arguments to the game log (separated by spaces)

#### Error

The default lua [`error` function](https://www.lua.org/manual/5.2/manual.html#pdf-error) is available and can be used to abort script execution.

#### Expect version

`void expect_version(int major)`
`void expect_version(int major, int min_minor)`

The `expect_version` function verifies if the script is compatible with the runtime version and informs the user about any incompatibilities.

The runtime should follow semantic versioning, where the major version should match and the minor version should match or be greater.

**This function can only be used once**

#### Raycast

`Vector3? raycast(RaycastPos raycast_pos)`
`Vector3? raycast(Vector3 origin)`
`Vector3? raycast(Vector3 origin, Vector3 direction)`
`Vector3? raycast(Vector3 origin, TRANSFORM relative_to)` ([TRANSFORM enum](#transform-objects))

The `raycast` function performs a raycast immediately, which collides with the same layers as an object being dropped.

In cases of missing arguments, the following defaults are used:

- `direction`: `VECTOR3_DOWN`
- `relative_to`: `TRANSFORM_SHIP`

**Repeated calls may be expensive, some sort of caching is recommended.**

#### Transform

`Vector3? transform(Vector3 point, TRANSFORM from, TRANSFORM to)` ([TRANSFORM enum](#transform-objects))

The `transform` function transforms a point from `from`-space to `to`-space immediately.

### Debug build

The release build of this mod is stripped of the script development helper tools.

Therefore, when developing a script, consider using a debug build of the mod.

Debug tools include, but are not limited to:

- Sort helper: displays the coordinates where you dropped an item (see `/sorthelper`)
- Execute from disk: executes the script straight from disk, instead of the cache (no need to reload your script after every change)
- Verbose logging: Prints every resulting item position in the log

<sub>Some features mentioned above only activate in a singleplayer LAN lobby</sub>

_There is currently no automated debug build, you'll have to compile the mod locally to use these._