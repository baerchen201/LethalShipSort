using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LethalShipSort;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI")]
public class LethalShipSort : BaseUnityPlugin
{
    public static LethalShipSort Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private ConfigEntry<int> configVersion = null!;
    public int ConfigVersion => configVersion.Value;
    public int CurrentConfigVersion => (int)configVersion.DefaultValue;

    private ConfigEntry<bool> autoSort = null!;
    public bool AutoSort
    {
        get => autoSort.Value;
        set => autoSort.Value = value;
    }

    private ConfigEntry<uint> sortDelay = null!;
    public uint SortDelay
    {
        get => sortDelay.Value;
        set => sortDelay.Value = value;
    }

    private ConfigEntry<string> defaultOneHand = null!;
    private ConfigEntry<string> defaultTwoHand = null!;
    private ConfigEntry<string> defaultTool = null!;
    private ConfigEntry<string> customItemPositions = null!;
    internal Dictionary<string, ConfigEntry<string>> itemPositions = new();
    internal Dictionary<string, string> vanillaItems = new();
    internal Dictionary<string, (Vector3, GameObject?)> roundOverrides = new();

    public ItemPosition DefaultOneHand
    {
        get
        {
            try
            {
                return new ItemPosition(defaultOneHand.Value);
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    $"Invalid DefaultOneHand position ({defaultOneHand.Value}), using fallback"
                );
                return new ItemPosition((string)defaultOneHand.DefaultValue);
            }
        }
        set => defaultOneHand.Value = value.ToString();
    }
    public ItemPosition DefaultTwoHand
    {
        get
        {
            try
            {
                return new ItemPosition(defaultTwoHand.Value);
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    $"Invalid DefaultTwoHand position ({defaultTwoHand.Value}), using fallback"
                );
                return new ItemPosition((string)defaultTwoHand.DefaultValue);
            }
        }
        set => defaultTwoHand.Value = value.ToString();
    }
    public ItemPosition DefaultTool
    {
        get
        {
            try
            {
                return new ItemPosition(defaultTool.Value);
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    $"Invalid DefaultTool position ({defaultTool.Value}), using fallback"
                );
                return new ItemPosition((string)defaultTool.DefaultValue);
            }
        }
        set => defaultTool.Value = value.ToString();
    }
    public Dictionary<string, ItemPosition> CustomItemPositions
    {
        get
        {
            Dictionary<string, ItemPosition> positions = new();
            foreach (var i in customItemPositions.Value.Split(';'))
            {
                string[] split = i.Split(":", 2);
                if (split.Length != 2)
                {
                    Logger.LogDebug("split.Length != 2");
                    goto skip;
                }

                if (positions.ContainsKey(split[0]))
                    Logger.LogWarning($"Multiple CustomItemPositions for item {split[0]}");
                try
                {
                    positions[split[0]] = new ItemPosition(split[1]);
                }
                catch (ArgumentException e)
                {
                    Logger.LogDebug($"{split[0]} {split[1]} {e}");
                    goto skip;
                }
                continue;

                skip:
                Logger.LogError($"Invalid CustomItemPosition ({i})");
            }
            return positions;
        }
    }
    public Dictionary<string, ItemPosition?> VanillaItemPositions
    {
        get
        {
            Dictionary<string, ItemPosition?> positions = new();
            foreach (var kvp in itemPositions)
            {
                try
                {
                    positions[kvp.Key] = string.IsNullOrWhiteSpace(kvp.Value.Value)
                        ? null
                        : new ItemPosition(kvp.Value.Value);
                }
                catch (ArgumentException)
                {
                    Logger.LogError(
                        $"Invalid item position for {kvp.Key} ({kvp.Value.Value}), using fallback"
                    );
                    positions[kvp.Key] =
                        (string)kvp.Value.DefaultValue == ""
                            ? null
                            : new ItemPosition((string)kvp.Value.DefaultValue);
                }
            }
            return positions;
        }
    }

    public ItemPosition GetPosition(GrabbableObject item)
    {
        var itemName = Utils.ItemKey(item);
        ItemPosition? itemPosition = null;

        Logger.LogDebug(
            $">> GetPosition({item}) itemName:{itemName} isScrap:{item.itemProperties.isScrap} twoHanded:{item.itemProperties.twoHanded}"
        );

        if (roundOverrides.TryGetValue(itemName.ToLower(), out var itemPositionOverride))
        {
            itemPosition = new ItemPosition
            {
                position = itemPositionOverride.Item1,
                parentTo = itemPositionOverride.Item2,
            };
            Logger.LogDebug(
                $"<< GetPosition (roundOverrides) {itemPosition} ({itemPositionOverride.Item1}, {itemPositionOverride.Item2})"
            );
        }
        else if (itemPositions.TryGetValue(itemName, out var itemPositionConfig))
            try
            {
                if (!itemPositionConfig.Value.IsNullOrWhiteSpace())
                {
                    itemPosition = new ItemPosition(itemPositionConfig.Value);
                    Logger.LogDebug(
                        $"<< GetPosition (itemPositions) {itemPosition} ({itemPositionConfig.Value})"
                    );
                }
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    $"Invalid item position for {itemName} ({itemPositionConfig.Value}), using fallback"
                );
                if (!((string)itemPositionConfig.DefaultValue).IsNullOrWhiteSpace())
                {
                    itemPosition = new ItemPosition((string)itemPositionConfig.DefaultValue);
                    Logger.LogDebug(
                        $"<< GetPosition (itemPositions - DefaultValue) {itemPosition} ({itemPositionConfig.DefaultValue})"
                    );
                }
            }
        else if (CustomItemPositions.TryGetValue(itemName, out var _itemPosition))
        {
            itemPosition = _itemPosition;
            Logger.LogDebug($"<< GetPosition (CustomItemPositions) {itemPosition}");
        }

        if (itemPosition == null)
        {
            itemPosition = item.itemProperties.isScrap
                ? item.itemProperties.twoHanded
                    ? DefaultTwoHand
                    : DefaultOneHand
                : DefaultTool;
            Logger.LogDebug(
                $"<< GetPosition ({(item.itemProperties.isScrap
                    ? item.itemProperties.twoHanded
                        ? "DefaultTwoHand"
                        : "DefaultOneHand"
                    : "DefaultTool")}) {itemPosition}"
            );
        }
        else if (itemPosition.Value.position == null)
        {
            var defaultItemPosition = item.itemProperties.isScrap
                ? item.itemProperties.twoHanded
                    ? DefaultTwoHand
                    : DefaultOneHand
                : DefaultTool;
            itemPosition = new ItemPosition
            {
                flags =
                    itemPosition.Value.flags.FilterFilteringRelated()
                    | defaultItemPosition.flags.FilterPositionRelated(),
                position = defaultItemPosition.position,
                parentTo = defaultItemPosition.parentTo,
            };
            Logger.LogDebug(
                $"<< GetPosition ({(item.itemProperties.isScrap
                    ? item.itemProperties.twoHanded
                        ? "DefaultTwoHand"
                        : "DefaultOneHand"
                    : "DefaultTool")} with flags) {itemPosition}"
            );
        }

        return itemPosition.Value;
    }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        configVersion = Config.Bind(
            "General",
            "ConfigVersion",
            5,
            "The version of this config file"
        );
        autoSort = Config.Bind(
            "General",
            "AutoSort",
            false,
            "Whether to automatically sort the ship when leaving a planet (toggle ingame with /autosort)"
        );
        sortDelay = Config.Bind(
            "General",
            "SortDelay",
            (uint)0,
            "The amount of milliseconds to wait between moving items, mostly for the satisfying visual effect.\nYou can't pick anything up while sorting items."
        );

        BindItemPositionConfigs();
        Patch();

        _ = new SortItemsCommand();
        _ = new AutoSortToggle();
        _ = new SetItemPositionCommand();
        _ = new PrintItemNames();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        return;

        void Patch()
        {
            Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);
            Logger.LogDebug("Patching...");
            Harmony.PatchAll();
            Logger.LogDebug("Finished patching!");
        }
    }

    private const float CUPBOARD_ABOVE = 3.2f;
    private const float CUPBOARD_TOP = 2.4f;
    private const float CUPBOARD_MIDDLE_1 = 2f;
    private const float CUPBOARD_MIDDLE_2 = 1.5f;
    private const float CUPBOARD_BOTTOM = 0.5f;

    private void BindItemPositionConfigs()
    {
        // Item globals
        defaultOneHand = Config.Bind(
            "Items",
            "DefaultOneHand",
            "-2.25,3,-5.25,0.05",
            "Default position for one-handed items."
        );
        defaultTwoHand = Config.Bind(
            "Items",
            "DefaultTwoHand",
            "-4.5,3,-5.25,0.05",
            "Default position for two-handed items."
        );
        defaultTool = Config.Bind(
            "Items",
            "DefaultTool",
            $"cupboard:-2,0.6,{CUPBOARD_BOTTOM},0,0.02:{ItemPosition.Flags.KEEP_ON_CRUISER}{ItemPosition.Flags.PARENT}",
            "Default position for tool items."
        );

        // Scrap
        ItemPositionConfig("Airhorn");
        ItemPositionConfig("LungApparatus", "Apparatice", new Vector3(-6.8f, 4.4f, -6.65f));
        itemPositions["LungApparatusTurnedOff"] = itemPositions["LungApparatus"];
        ItemPositionConfig("HandBell", "Brass bell");
        ItemPositionConfig("BigBolt", "Big bolt");
        ItemPositionConfig("Bone");
        ItemPositionConfig("BinFullOfBottles", "Bottles");
        ItemPositionConfig("Hairbrush", "Hair brush");
        ItemPositionConfig("Candy");
        ItemPositionConfig("CashRegisterItem", "Cash register");
        ItemPositionConfig("ChemicalJug", "Chemical jug");
        ItemPositionConfig("Clock");
        ItemPositionConfig("Clownhorn", "Clown horn");
        ItemPositionConfig("ComedyMask", "Comedy");
        ItemPositionConfig("ControlPad", "Control pad");
        ItemPositionConfig("CookieMoldPan", "Cookie mold pan");
        ItemPositionConfig("Dustpan", "Dust pan");
        ItemPositionConfig("Ear");
        ItemPositionConfig("EasterEgg", "Easter egg");
        ItemPositionConfig("KiwiBabyItem", "Egg", new Vector3(4.85f, 2f, -4));
        ItemPositionConfig("EggBeater", "Egg beater");
        ItemPositionConfig("FancyLamp", "Fancy lamp");
        ItemPositionConfig("Flask");
        ItemPositionConfig("SeveredFootLOD0", "Foot");
        ItemPositionConfig("GarbageLid", "Garbage lid");
        ItemPositionConfig("GiftBox", "Gift box");
        ItemPositionConfig("GoldBar", "Gold Bar");
        ItemPositionConfig("FancyGlass", "Golden cup");
        ItemPositionConfig("SeveredHandLOD0", "Hand");
        ItemPositionConfig("HeartContainer", "Heart");
        ItemPositionConfig("Hairdryer");
        ItemPositionConfig("RedLocustHive", "Bee hive", new Vector3(-6.8f, 4.4f, -5.65f));
        ItemPositionConfig("DiyFlashbang", "Homemade Flashbang");
        ItemPositionConfig("PickleJar", "Jar of pickles");
        ItemPositionConfig(
            "KnifeItem",
            "Kitchen knife",
            new Vector3(-1.9f, 0.6f, CUPBOARD_MIDDLE_2),
            defaultInCupboard: true,
            defaultKeepOnCruiser: true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig("SeveredThighLOD0", "Knee");
        ItemPositionConfig("Cog", "Large axle");
        ItemPositionConfig("LaserPointer", "Laser pointer");
        ItemPositionConfig("Magic7Ball", "Magic 7 ball");
        ItemPositionConfig("MagnifyingGlass", "Magnifying glass");
        ItemPositionConfig("MetalSheet", "Tattered metal sheet");
        ItemPositionConfig("Mug", "Coffee mug");
        ItemPositionConfig("OldPhone", "Old phone");
        ItemPositionConfig("Painting");
        ItemPositionConfig("PerfumeBottle", "Perfume bottle");
        ItemPositionConfig("PillBottle", "Pill bottle");
        ItemPositionConfig("PlasticCup", "Plastic cup");
        ItemPositionConfig("FishTestProp", "Plastic fish");
        ItemPositionConfig("RedSodaCan", "Red soda");
        ItemPositionConfig("Remote");
        ItemPositionConfig("RibcageBone", "Ribcage");
        ItemPositionConfig("FancyRing", "Wedding ring");
        ItemPositionConfig("RubberDucky", "Rubber ducky");
        ItemPositionConfig(
            "ShotgunItem",
            "Double-barrel",
            new Vector3(8.75f, 2f, -5.5f),
            defaultKeepOnCruiser: true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig("SoccerBall", "Soccer ball", new Vector3(-6.8f, 4.4f, -7.75f));
        ItemPositionConfig("SteeringWheel", "Steering wheel");
        ItemPositionConfig("StopSign", "Stop sign");
        ItemPositionConfig("TeaKettle", "Tea Kettle");
        ItemPositionConfig("Dentures", "Teeth");
        ItemPositionConfig("ToiletPaperRolls", "Toilet paper");
        ItemPositionConfig("Toothpaste");
        ItemPositionConfig("Tongue");
        ItemPositionConfig("ToyCube", "Toy cube");
        ItemPositionConfig("RobotToy", "Robot Toy");
        ItemPositionConfig("ToyTrain", "Toy train");
        ItemPositionConfig("TragedyMask", "Tragedy");
        ItemPositionConfig("EnginePart", "V-type engine");
        ItemPositionConfig("WhoopieCushion", "Whoopie cushion", new Vector3(9f, 2f, -8.25f));
        ItemPositionConfig("YieldSign", "Yield sign");
        ItemPositionConfig("ZeddogPlushie", "Zed Dog", new Vector3(9f, 1.21f, -5.55f));

        // Tools
        ItemPositionConfig(
            "WalkieTalkie",
            "Walkie-talkie",
            new Vector3(-1.4f, 0.6f, CUPBOARD_TOP),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "BBFlashlight",
            "Flashlight",
            new Vector3(-1.3f, 0.2f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "ShovelItem",
            "Shovel",
            new Vector3(-1.5f, 0.3f, CUPBOARD_MIDDLE_2),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "LockPickerItem",
            "Lockpicker",
            new Vector3(-2f, 0.5f, CUPBOARD_TOP),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "FlashlightItem",
            "Pro-flashlight",
            new Vector3(-1.3f, 0.65f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "StunGrenade",
            "Stun grenade",
            new Vector3(-1.2f, 0.5f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "Boombox",
            new Vector3(-0.3f, 0.5f, CUPBOARD_ABOVE),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "TZPChemical",
            "TZP-Inhalant",
            new Vector3(-0.55f, 0.2f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "PatcherGunItem",
            "Zap gun",
            new Vector3(-1.1f, 0.6f, CUPBOARD_TOP),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "JetpackItem",
            "Jetpack",
            new Vector3(-0.3f, 0.2f, CUPBOARD_BOTTOM),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig("ExtensionLadderItem", "Extension ladder", true);
        ItemPositionConfig("RadarBoosterDevice", "Radar booster", true);
        ItemPositionConfig(
            "SprayPaintItem",
            "Spray paint",
            new Vector3(-1.7f, 0.5f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "WeedKillerItem",
            "Weed killer",
            new Vector3(-2.05f, 0.5f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "BeltBagItem",
            "Belt bag",
            new Vector3(-0.35f, 0.5f, CUPBOARD_TOP - 0.1f),
            true,
            defaultFloorYRot: 0
        );
        // ItemPositionConfig("CompanyCruiser", "Company Cruiser"); // :)

        ItemPositionConfig(
            "Key",
            new Vector3(-0.3f, 0.6f, CUPBOARD_MIDDLE_2),
            true,
            defaultFloorYRot: 0
        );
        ItemPositionConfig(
            "ShotgunShell",
            "Shotgun Shell",
            new Vector3(-0.3f, 0.6f, CUPBOARD_MIDDLE_1),
            true,
            defaultFloorYRot: 0
        );

        // Custom item positions
        customItemPositions = Config.Bind(
            "Items",
            "CustomItemPositions",
            "MyItem1:0,0,0;MyItem2:cupboard:1.5,-2,3",
            "Semicolon-separated list of internal item names and their positions."
        );
    }

    private void ItemPositionConfig(
        string internalName,
        string itemName,
        bool isTool = false,
        bool? defaultKeepOnCruiser = null
    )
    {
        itemPositions[internalName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            internalName,
            $"{(defaultKeepOnCruiser ?? isTool ? ItemPosition.Flags.KEEP_ON_CRUISER : string.Empty)}",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = internalName.ToLower();
    }

    private void ItemPositionConfig(
        string itemName,
        bool isTool = false,
        bool? defaultKeepOnCruiser = null
    )
    {
        itemPositions[itemName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            itemName,
            $"{(defaultKeepOnCruiser ?? isTool ? ItemPosition.Flags.KEEP_ON_CRUISER : string.Empty)}",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = itemName.ToLower();
    }

    private static string Flags(bool keepOnCruiser, bool inCupboard) =>
        keepOnCruiser || inCupboard
            ? $":{(keepOnCruiser ? ItemPosition.Flags.KEEP_ON_CRUISER : string.Empty)}{
                (inCupboard ? ItemPosition.Flags.PARENT : string.Empty)}"
            : string.Empty;

    private void ItemPositionConfig(
        string internalName,
        string itemName,
        Vector3 defaultPosition,
        bool isTool = false,
        bool? defaultInCupboard = null,
        bool? defaultKeepOnCruiser = null,
        int? defaultFloorYRot = null
    )
    {
        itemPositions[internalName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            internalName,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1},{2},{3}{4}{5}",
                defaultInCupboard ?? isTool ? "cupboard:" : "",
                defaultPosition.x,
                defaultPosition.y,
                defaultPosition.z,
                defaultFloorYRot != null
                    ? string.Format(CultureInfo.InvariantCulture, ",{0}", defaultFloorYRot)
                    : string.Empty,
                Flags(defaultKeepOnCruiser ?? isTool, defaultInCupboard ?? isTool)
            ),
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = internalName.ToLower();
    }

    private void ItemPositionConfig(
        string itemName,
        Vector3 defaultPosition,
        bool isTool = false,
        bool? defaultInCupboard = null,
        bool? defaultKeepOnCruiser = null,
        int? defaultFloorYRot = null
    )
    {
        itemPositions[itemName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            itemName,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1},{2},{3}{4}{5}",
                defaultInCupboard ?? isTool ? "cupboard:" : "",
                defaultPosition.x,
                defaultPosition.y,
                defaultPosition.z,
                defaultFloorYRot != null
                    ? string.Format(CultureInfo.InvariantCulture, ",{0}", defaultFloorYRot)
                    : string.Empty,
                Flags(defaultKeepOnCruiser ?? isTool, defaultInCupboard ?? isTool)
            ),
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = itemName.ToLower();
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartServer))]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    internal class RoundOverrideResetPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            var i = Instance.roundOverrides.Count;
            Instance.roundOverrides.Clear();
            Logger.LogDebug($"roundOverrides cleared (was {i} items)");
        }
    }

    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.Start))]
    internal class HUDManager_Start
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            if (Instance.ConfigVersion < Instance.CurrentConfigVersion)
            {
                ChatCommandAPI.ChatCommandAPI.PrintWarning(
                    $"[{MyPluginInfo.PLUGIN_NAME}] Your configuration file is outdated.\n"
                        + $"Please verify your configuration file and update it accordingly.\n"
                        + $"<indent=10px>Expected: v{Instance.CurrentConfigVersion}\n"
                        + $"Config: v{Instance.ConfigVersion}</indent>"
                );
                Logger.LogWarning(
                    $"Config file outdated: Expected v{Instance.CurrentConfigVersion}, got v{Instance.ConfigVersion}"
                );
            }
            else if (Instance.ConfigVersion > Instance.CurrentConfigVersion)
            {
                ChatCommandAPI.ChatCommandAPI.PrintWarning(
                    $"[{MyPluginInfo.PLUGIN_NAME}] Your configuration file is using a newer, unsupported format.\n"
                        + $"There have been changes to the configuration file format which could cause errors.\n"
                        + $"Please verify your configuration file and mod version.\n"
                        + $"<indent=10px>Expected: v{Instance.CurrentConfigVersion}\n"
                        + $"Config: v{Instance.ConfigVersion}</indent>"
                );
                Logger.LogWarning(
                    $"Config file unsupported: Expected v{Instance.CurrentConfigVersion}, got v{Instance.ConfigVersion}"
                );
            }
        }
    }
}
