using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChatCommandAPI;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace LethalShipSort;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI")]
public class LethalShipSort : BaseUnityPlugin
{
    public static LethalShipSort Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private ConfigEntry<bool> autoSort = null!;
    public bool AutoSort
    {
        get => autoSort.Value;
        set => autoSort.Value = value;
    }
    private ConfigEntry<string> excludeItems = null!;
    public string[] ExcludeItems
    {
        get => excludeItems.Value.Split(",");
        set => excludeItems.Value = value.Join(null, ",");
    }

    private ConfigEntry<bool> useRaycast = null!;
    private ConfigEntry<uint> sortDelay = null!;

    public bool UseRaycast
    {
        get => useRaycast.Value;
        set => useRaycast.Value = value;
    }
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
            foreach (string i in customItemPositions.Value.Split(';'))
            {
                string[] split = i.Split(':', 2);
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
        set =>
            customItemPositions.Value = value
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .Join(null, ";");
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
                    positions[kvp.Key] =
                        kvp.Value.Value == "" ? null : new ItemPosition(kvp.Value.Value);
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
        string itemName = Utils.RemoveClone(item.name);
        ItemPosition? itemPosition = null;
        if (roundOverrides.TryGetValue(itemName.ToLower(), out var itemPositionOverride))
            itemPosition = new ItemPosition
            {
                position = itemPositionOverride.Item1,
                parentTo = itemPositionOverride.Item2,
            };
        else if (itemPositions.TryGetValue(itemName, out var itemPositionConfig))
            try
            {
                if (!itemPositionConfig.Value.IsNullOrWhiteSpace())
                    itemPosition = new ItemPosition(itemPositionConfig.Value);
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    $"Invalid item position for {itemName} ({itemPositionConfig.Value}), using fallback"
                );
                if (!((string)itemPositionConfig.DefaultValue).IsNullOrWhiteSpace())
                    itemPosition = new ItemPosition((string)itemPositionConfig.DefaultValue);
            }
        else if (CustomItemPositions.TryGetValue(itemName, out var _itemPosition))
            itemPosition = _itemPosition;

        itemPosition ??= item.itemProperties.isScrap
            ? item.itemProperties.twoHanded
                ? DefaultTwoHand
                : DefaultOneHand
            : DefaultTool;

        return itemPosition.Value;
    }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        autoSort = Config.Bind(
            "General",
            "AutoSort",
            false,
            "Whether to automatically sort the ship when leaving a planet (toggle ingame with /autosort)"
        );
        useRaycast = Config.Bind(
            "General",
            "UseRaycast",
            true,
            "If enabled, items will be put on the closest surface below the given position, instead of the exact coordinates"
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

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private const float CUPBOARD_ABOVE = 3.2f;
    private const float CUPBOARD_TOP = 2.4f;
    private const float CUPBOARD_MIDDLE_1 = 2f;
    private const float CUPBOARD_MIDDLE_2 = 1.5f;
    private const float CUPBOARD_BOTTOM = 0.5f;

    private void BindItemPositionConfigs()
    {
        // Item globals
        excludeItems = Config.Bind(
            "Items",
            "ExcludeItems",
            "",
            "Comma-separated list of item names to never sort (by internal name)"
        );
        defaultOneHand = Config.Bind(
            "Items",
            "DefaultOneHand",
            "-2.25,2,-5.25",
            "Default position for one-handed items."
        );
        defaultTwoHand = Config.Bind(
            "Items",
            "DefaultTwoHand",
            "-4.5,3,-5.25",
            "Default position for two-handed items."
        );
        defaultTool = Config.Bind(
            "Items",
            "DefaultTool",
            $"cupboard:-2,0.6,{CUPBOARD_BOTTOM}",
            "Default position for tool items."
        );

        // Scrap
        ItemPositionConfig("Airhorn");
        ItemPositionConfig("LungApparatus", "Apparatice", new Vector3(-6.8f, 4.4f, -6.65f));
        itemPositions["LungApparatusTurnedOff"] = itemPositions["LungApparatus"];
        ItemPositionConfig("HandBell", "Brass bell");
        ItemPositionConfig("BigBolt", "Big bolt");
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
        ItemPositionConfig("EasterEgg", "Easter egg");
        ItemPositionConfig("KiwiBabyItem", "Egg", new Vector3(4.85f, 2f, -4));
        ItemPositionConfig("EggBeater", "Egg beater");
        ItemPositionConfig("FancyLamp", "Fancy lamp");
        ItemPositionConfig("Flask");
        ItemPositionConfig("GarbageLid", "Garbage lid");
        ItemPositionConfig("GiftBox", "Gift box");
        ItemPositionConfig("GoldBar", "Gold Bar");
        ItemPositionConfig("FancyGlass", "Golden cup");
        ItemPositionConfig("Hairdryer");
        ItemPositionConfig("RedLocustHive", "Bee hive", new Vector3(-6.8f, 4.4f, -5.65f));
        ItemPositionConfig("DiyFlashbang", "Homemade Flashbang");
        ItemPositionConfig("PickleJar", "Jar of pickles");
        ItemPositionConfig("KnifeItem", "Kitchen knife");
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
        ItemPositionConfig("FancyRing", "Wedding ring");
        ItemPositionConfig("RubberDucky", "Rubber ducky");
        ItemPositionConfig("ShotgunItem", "Double-barrel", new Vector3(8.75f, 2f, -5.5f));
        ItemPositionConfig("SoccerBall", "Soccer ball", new Vector3(-6.8f, 4.4f, -7.75f));
        ItemPositionConfig("SteeringWheel", "Steering wheel");
        ItemPositionConfig("StopSign", "Stop sign");
        ItemPositionConfig("TeaKettle", "Tea Kettle");
        ItemPositionConfig("Dentures", "Teeth");
        ItemPositionConfig("ToiletPaperRolls", "Toilet paper");
        ItemPositionConfig("Toothpaste");
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
            true
        );
        ItemPositionConfig(
            "BBFlashlight",
            "Flashlight",
            new Vector3(-1.3f, 0.2f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig(
            "ShovelItem",
            "Shovel",
            new Vector3(-1.5f, 0.3f, CUPBOARD_MIDDLE_2),
            true
        );
        ItemPositionConfig(
            "LockPickerItem",
            "Lockpicker",
            new Vector3(-2f, 0.5f, CUPBOARD_TOP),
            true
        );
        ItemPositionConfig(
            "FlashlightItem",
            "Pro-flashlight",
            new Vector3(-1.3f, 0.65f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig(
            "StunGrenade",
            "Stun grenade",
            new Vector3(-1.2f, 0.5f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig("Boombox", new Vector3(-0.3f, 0.5f, CUPBOARD_ABOVE), true);
        ItemPositionConfig(
            "TZPChemical",
            "TZP-Inhalant",
            new Vector3(-0.55f, 0.2f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig(
            "PatcherGunItem",
            "Zap gun",
            new Vector3(-1.1f, 0.6f, CUPBOARD_TOP),
            true
        );
        ItemPositionConfig(
            "JetpackItem",
            "Jetpack",
            new Vector3(-0.3f, 0.2f, CUPBOARD_BOTTOM),
            true
        );
        ItemPositionConfig("ExtensionLadderItem", "Extension ladder", true);
        ItemPositionConfig("RadarBoosterDevice", "Radar booster", true);
        ItemPositionConfig(
            "SprayPaintItem",
            "Spray paint",
            new Vector3(-1.7f, 0.5f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig(
            "WeedKillerItem",
            "Weed killer",
            new Vector3(-2.05f, 0.5f, CUPBOARD_MIDDLE_1),
            true
        );
        ItemPositionConfig(
            "BeltBagItem",
            "Belt bag",
            new Vector3(-0.35f, 0.5f, CUPBOARD_TOP - 0.1f),
            true
        );
        // ItemPositionConfig("CompanyCruiser", "Company Cruiser"); // :)

        ItemPositionConfig("Key", new Vector3(-0.3f, 0.6f, CUPBOARD_MIDDLE_2), true);
        ItemPositionConfig(
            "ShotgunShell",
            "Shotgun Shell",
            new Vector3(-0.3f, 0.6f, CUPBOARD_MIDDLE_1),
            true
        );

        // Custom item positions
        customItemPositions = Config.Bind(
            "Items",
            "CustomItemPositions",
            "MyItem1:0,0,0;MyItem2:cupboard:1.5,-2,3",
            "Semicolon-separated list of internal item names and their positions."
        );
    }

    [Obsolete]
    private ConfigEntry<string> BindItemPositionConfig(string itemName) =>
        Config.Bind("Items", itemName, "", $"Position for the {itemName} item.");

    private void ItemPositionConfig(string internalName, string itemName, bool isTool = false)
    {
        itemPositions[internalName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            internalName,
            "",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = internalName.ToLower();
    }

    private void ItemPositionConfig(string itemName, bool isTool = false)
    {
        itemPositions[itemName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            itemName,
            "",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = itemName.ToLower();
    }

    private void ItemPositionConfig(
        string internalName,
        string itemName,
        Vector3 defaultPosition,
        bool isTool = false,
        bool? defaultInCupboard = null
    )
    {
        itemPositions[internalName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            internalName,
            $"{(defaultInCupboard ?? isTool ? "cupboard:" : "")}{Math.Round(defaultPosition.x, 2)},{Math.Round(defaultPosition.y, 2)},{Math.Round(defaultPosition.z, 2)}",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = internalName.ToLower();
    }

    private void ItemPositionConfig(
        string itemName,
        Vector3 defaultPosition,
        bool isTool = false,
        bool? defaultInCupboard = null
    )
    {
        itemPositions[itemName] = Config.Bind(
            isTool ? "Tools" : "Scrap",
            itemName,
            $"{(defaultInCupboard ?? isTool ? "cupboard:" : "")}{Math.Round(defaultPosition.x, 2)},{Math.Round(defaultPosition.y, 2)},{Math.Round(defaultPosition.z, 2)}",
            $"Position for the {itemName} item."
        );
        vanillaItems[itemName.ToLower()] = itemName.ToLower();
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartServer))]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    internal class RoundOverrideResetPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            int i = LethalShipSort.Instance.roundOverrides.Count;
            LethalShipSort.Instance.roundOverrides.Clear();
            Logger.LogDebug(
                $"roundOverrides cleared (was {i} items, now {LethalShipSort.Instance.roundOverrides.Count})"
            );
        }
    }
}

public class SortItemsCommand : Command
{
    public override string Name => "SortItems";
    public override string[] Commands => [Name, "Sort", "Organize"];
    public override string Description =>
        "Sorts all items on the ship\n-a: sort all items, even items on cruiser";
    public override string[] Syntax =>
        ["", "[ -a | --all ]", "<item> { here | there } [ once | game | always ]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        return StartOfRound.Instance.inShipPhase
            && (
                args.Length < 2
                    ? SortAllItems(args.Contains("-a") || args.Contains("--all"), out error)
                    : SetItemPositionCommand.SetItemPosition(args, out error)
            );
    }

    internal static Coroutine? sorting;

    private static bool SortAllItems(bool all, out string error)
    {
        error = "No items to sort";
        GameNetworkManager.Instance.localPlayerController.DropAllHeldItemsAndSync();
        var items = Object.FindObjectsOfType<GrabbableObject>();
        if (items == null)
            return false;
        items = items
            .Where(i => i is { playerHeldBy: null } and not RagdollGrabbableObject)
            .ToArray();
        if (items.Length == 0)
            return false;

        ChatCommandAPI.ChatCommandAPI.Print("Sorting all items...");

        var cars = Object.FindObjectsOfType<VehicleController>() ?? [];

        var scrap = items
            .Where(i =>
                i.itemProperties.isScrap
                && (
                    all
                    || !(
                        Utils.RemoveClone(i.name) == "ShotgunItem"
                        && cars.Any(car => i.gameObject.transform.parent == car.transform)
                    )
                )
            )
            .ToArray();
        var tools = items
            .Where(i =>
                !i.itemProperties.isScrap
                && (
                    all
                    || cars.All(car =>
                        i.gameObject.transform.parent != car.gameObject.gameObject.transform
                    )
                )
            )
            .ToArray();
        if (LethalShipSort.Instance.SortDelay < 10)
        {
            int scrapFailed = 0;
            int toolsFailed = 0;

            if (scrap.Length != 0)
                scrapFailed = SortItems(scrap);
            if (tools.Length != 0)
                toolsFailed = SortItems(tools);

            error =
                $"{(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted";

            if (scrapFailed != 0 || toolsFailed != 0)
                return false;
            ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
        }
        else
        {
            if (sorting != null)
                GameNetworkManager.Instance.localPlayerController.StopCoroutine(sorting);
            sorting = GameNetworkManager.Instance.localPlayerController.StartCoroutine(
                SortAllItemsDelayed(LethalShipSort.Instance.SortDelay, scrap, tools)
            );
        }

        return true;
    }

    private static void AutoSortAllItems()
    {
        try
        {
            GameNetworkManager.Instance.localPlayerController.DropAllHeldItemsAndSync();
            var items = Object.FindObjectsOfType<GrabbableObject>();
            if (items == null)
                return;
            items = items
                .Where(i => i is { playerHeldBy: null } and not RagdollGrabbableObject)
                .ToArray();
            if (items.Length == 0)
                return;

            ChatCommandAPI.ChatCommandAPI.Print("Sorting all items...");

            var cars = Object.FindObjectsOfType<VehicleController>() ?? [];

            var scrap = items
                .Where(i =>
                    i.itemProperties.isScrap
                    && !(
                        Utils.RemoveClone(i.name) == "ShotgunItem"
                        && cars.Any(car => i.gameObject.transform.parent == car.transform)
                    )
                )
                .ToArray();
            var tools = items
                .Where(i =>
                    !i.itemProperties.isScrap
                    && cars.All(car =>
                        i.gameObject.transform.parent != car.gameObject.gameObject.transform
                    )
                )
                .ToArray();
            if (LethalShipSort.Instance.SortDelay < 10)
            {
                int scrapFailed = 0;
                int toolsFailed = 0;

                if (scrap.Length != 0)
                    scrapFailed = SortItems(scrap);
                if (tools.Length != 0)
                    toolsFailed = SortItems(tools);

                if (scrapFailed != 0 || toolsFailed != 0)
                    ChatCommandAPI.ChatCommandAPI.PrintError(
                        $"Automatic sorting failed: {(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted"
                    );
                else
                    ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
            }
            else
            {
                if (sorting != null)
                    GameNetworkManager.Instance.localPlayerController.StopCoroutine(sorting);
                sorting = GameNetworkManager.Instance.localPlayerController.StartCoroutine(
                    SortAllItemsDelayed(
                        LethalShipSort.Instance.SortDelay,
                        scrap,
                        tools,
                        "Automatic sorting failed"
                    )
                );
            }
        }
        catch (Exception e)
        {
            ChatCommandAPI.ChatCommandAPI.PrintError(
                "Automatic sorting failed due to an internal error, check the log for details"
            );
            LethalShipSort.Logger.LogError($"Error while autosorting items: {e}");
        }
    }

    public static IEnumerator SortAllItemsDelayed(
        uint delay,
        GrabbableObject[] scrap,
        GrabbableObject[] tools,
        string errorPrefix = "Error running command"
    )
    {
        int scrapFailed = 0;
        int toolsFailed = 0;

        foreach (var item in scrap)
        {
            try
            {
                if (!Utils.MoveItem(item))
                    scrapFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                scrapFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }
        foreach (var item in tools)
        {
            try
            {
                if (!Utils.MoveItem(item))
                    toolsFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                toolsFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }

        string error =
            $"{(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted";

        if (scrapFailed != 0 || toolsFailed != 0)
            ChatCommandAPI.ChatCommandAPI.PrintError(
                $"{errorPrefix}: <noparse>" + error + "</noparse>"
            );
        else
            ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
    }

    public static int SortItems(GrabbableObject[] items) =>
        items.Count(item =>
        {
            try
            {
                return !Utils.MoveItem(item);
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                return true;
            }
        });

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetShipReadyToLand))]
    internal static class AutoSortPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix()
        {
            if (
                LethalShipSort.Instance.AutoSort
                && GameNetworkManager.Instance.localPlayerController.isHostPlayerObject
            )
                AutoSortAllItems();
        }
    }
}

public class SetItemPositionCommand : Command
{
    public override string Name => "SetItemPosition";
    public override string Description => "Sets the position for an item when sorting";
    public override string[] Commands => ["put", Name];
    public override string[] Syntax =>
        [
            "\"<item>\" { here | there } [ once | game | always ]\nExample: /put \"easter egg\" there always",
        ];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        return StartOfRound.Instance.inShipPhase && SetItemPosition(args, out error);
    }

    public static bool SetItemPosition(string[] args, out string error)
    {
        error = "Invalid arguments";
        switch (args.Length)
        {
            case 2:
                return SetItemPosition(
                    args[0],
                    args[1].ToLower() switch
                    {
                        "here" => where.here,
                        "there" => where.there,
                        _ => where.error,
                    },
                    when.once,
                    out error
                );
            case 3:
                return SetItemPosition(
                    args[0],
                    args[1].ToLower() switch
                    {
                        "here" => where.here,
                        "there" => where.there,
                        _ => where.error,
                    },
                    args[2].ToLower() switch
                    {
                        "once" or "now" => when.once,
                        "game" or "round" => when.game,
                        "always" or "save" => when.always,
                        _ => when.error,
                    },
                    out error
                );
            default:
                return false;
        }
    }

    public enum where
    {
        here,
        there,
        error,
    }

    public enum when
    {
        once,
        game,
        always,
        error,
    }

    public static bool SetItemPosition(string name, where where, when when, out string error)
    {
        error = "Invalid arguments";
        if (where == where.error || when == when.error)
            return false;
        error = "Invalid item name";
        if (!LethalShipSort.Instance.vanillaItems.TryGetValue(name.ToLower(), out var internalName))
            if (LethalShipSort.Instance.vanillaItems.ContainsValue(name.ToLower()))
                internalName = name;
            else
                return false;
        LethalShipSort.Logger.LogDebug($"internalName: {internalName} ({name})");

        ConfigEntry<string>? config = null;
        if (when == when.always)
        {
            if (
                LethalShipSort.Instance.itemPositions.All(kvp =>
                    !string.Equals(kvp.Key, internalName, StringComparison.CurrentCultureIgnoreCase)
                )
            )
                return false;
            config = LethalShipSort
                .Instance.itemPositions.First(kvp =>
                    string.Equals(kvp.Key, internalName, StringComparison.CurrentCultureIgnoreCase)
                )
                .Value;
        }

        error = "Error getting position";
        if (!GetPosition(where, out var position, out var relativeTo))
            return false;
        LethalShipSort.Logger.LogDebug(
            $"position: {position} relative to {relativeTo} ({(relativeTo == null ? "null" : Utils.GameObjectPath(relativeTo))}, {(relativeTo == null ? "" : GameObject.Find(Utils.GameObjectPath(relativeTo)))})"
        );
        switch (when)
        {
            case when.once:
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"Moving all items of type {internalName} to position {(relativeTo == null ? position : relativeTo.transform.InverseTransformPoint(position))}"
                );
                error = "No items to sort";
                var items = Object.FindObjectsOfType<GrabbableObject>();
                if (items == null)
                    return false;
                items = items
                    .Where(i =>
                        i is { playerHeldBy: null } and not RagdollGrabbableObject
                        && string.Equals(
                            Utils.RemoveClone(i.name),
                            internalName,
                            StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                    .ToArray();
                if (items.Length == 0)
                    return false;

                if (LethalShipSort.Instance.SortDelay < 10)
                {
                    int itemsFailed = items.Count(item =>
                    {
                        try
                        {
                            return !Utils.MoveItem(
                                item,
                                new ItemPosition { position = position, parentTo = relativeTo }
                            );
                        }
                        catch (Exception e)
                        {
                            LethalShipSort.Logger.LogError(e);
                            return true;
                        }
                    });
                    error = $"{itemsFailed} items couldn't be sorted";
                    ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
                    return itemsFailed == 0;
                }
                else
                {
                    if (SortItemsCommand.sorting != null)
                        GameNetworkManager.Instance.localPlayerController.StopCoroutine(
                            SortItemsCommand.sorting
                        );
                    SortItemsCommand.sorting =
                        GameNetworkManager.Instance.localPlayerController.StartCoroutine(
                            SortItemsDelayed(
                                LethalShipSort.Instance.SortDelay,
                                items,
                                position,
                                relativeTo
                            )
                        );
                    return true;
                }

            case when.game:
                LethalShipSort.Instance.roundOverrides[internalName.ToLower()] = (
                    position,
                    relativeTo
                );
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"Items of type {internalName} will be put on position {(relativeTo == null ? position : relativeTo.transform.InverseTransformPoint(position))} for this game"
                );
                break;
            case when.always:
                config!.Value =
                    relativeTo == null
                        ? $"none:{position.x},{position.y},{position.z}"
                        : $"{Utils.GameObjectPath(relativeTo)}:{position.x},{position.y},{position.z}";
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"Items of type {internalName} will be put on position {(relativeTo == null ? position : relativeTo.transform.InverseTransformPoint(position))}"
                );
                return true;
        }

        return true;
    }

    private static IEnumerator SortItemsDelayed(
        uint delay,
        GrabbableObject[] items,
        Vector3 position,
        GameObject? relativeTo,
        string errorPrefix = "Error running command"
    )
    {
        int itemsFailed = 0;

        foreach (var item in items)
        {
            try
            {
                if (
                    !Utils.MoveItem(
                        item,
                        new ItemPosition { position = position, parentTo = relativeTo }
                    )
                )
                    itemsFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                itemsFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }

        string error = $"{itemsFailed} items couldn't be sorted";

        if (itemsFailed != 0)
            ChatCommandAPI.ChatCommandAPI.PrintError(
                $"{errorPrefix}: <noparse>" + error + "</noparse>"
            );
        else
            ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
    }

    private static bool GetPosition(where where, out Vector3 position, out GameObject? relativeTo)
    {
        position = default;
        relativeTo = null!;
        switch (where)
        {
            case where.here:
                if (
                    Physics.Raycast(
                        GameNetworkManager.Instance.localPlayerController.transform.position,
                        Vector3.down,
                        out var hitInfo,
                        80f,
                        (int)(
                            Utils.Layers.Room
                            | Utils.Layers.InteractableObject
                            | Utils.Layers.Colliders
                            | Utils.Layers.PlaceableShipObjects
                        ),
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    position = hitInfo.collider.gameObject.transform.InverseTransformPoint(
                        hitInfo.point + new Vector3(0, 0.2f, 0)
                    );
                    relativeTo = hitInfo.collider.gameObject;
                    return true;
                }
                break;
            case where.there:
                if (
                    Physics.Raycast(
                        GameNetworkManager
                            .Instance
                            .localPlayerController
                            .gameplayCamera
                            .transform
                            .position,
                        GameNetworkManager
                            .Instance
                            .localPlayerController
                            .gameplayCamera
                            .transform
                            .forward,
                        out hitInfo,
                        80f,
                        (int)(
                            Utils.Layers.Room
                            | Utils.Layers.InteractableObject
                            | Utils.Layers.Colliders
                            | Utils.Layers.PlaceableShipObjects
                        ),
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    position = hitInfo.collider.gameObject.transform.InverseTransformPoint(
                        hitInfo.point + new Vector3(0, 0.2f, 0)
                    );
                    relativeTo = hitInfo.collider.gameObject;
                    return true;
                }
                break;
        }
        return false;
    }
}

[Obsolete]
internal class PrintLayerMasks : Command
{
    public override bool Hidden => true;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        LethalShipSort.Logger.LogInfo("LayerMask MoveItemRelativeTo:");
        for (int i = 0; i < 32; i++)
        {
            LethalShipSort.Logger.LogInfo(
                $" {((268437760 & (1 << i)) != 0 ? "o" : "-")} {LayerMask.LayerToName(i)}"
            );
        }
        LethalShipSort.Logger.LogInfo("LayerMask MoveItem:");
        for (int i = 0; i < 32; i++)
        {
            LethalShipSort.Logger.LogInfo(
                $" {((1073744640 & (1 << i)) != 0 ? "o" : "-")} {LayerMask.LayerToName(i)}"
            );
        }

        ChatCommandAPI.ChatCommandAPI.Print("layer masks printed to log");
        error = null!;
        return true;
    }
}

public class AutoSortToggle : ToggleCommand
{
    public override string Name => "AutoSort";
    public override string ToggleDescription =>
        "Toggles automatic item sorting when leaving a planet";

    public override bool Value
    {
        get => LethalShipSort.Instance.AutoSort;
        set => LethalShipSort.Instance.AutoSort = value;
    }
}

public static class Utils
{
    private const string CLONE = "(Clone)";

    public static string RemoveClone(string name) =>
        name.EndsWith(CLONE) ? name[..^CLONE.Length] : name;

    public static bool MoveItem(GrabbableObject item) =>
        LethalShipSort.Instance.ExcludeItems.Contains(RemoveClone(item.name))
        || MoveItem(item, LethalShipSort.Instance.GetPosition(item));

    public static bool MoveItem(GrabbableObject item, ItemPosition position) =>
        position.parentTo == GameObject.Find("Environment/HangarShip/StorageCloset")
            ? MoveItem(item, position.position, position.parentTo)
            : MoveItemRelativeTo(item, position.position, position.parentTo);

    public static bool MoveItemRelativeTo(
        GrabbableObject item,
        Vector3 position,
        GameObject? relativeTo
    )
    {
        LethalShipSort.Logger.LogDebug(
            $">> Moving item {RemoveClone(item.name)} to position {position} relative to {(relativeTo == null ? "ship" : RemoveClone(relativeTo.name))}"
        );

        GameObject ship = GameObject.Find("Environment/HangarShip");
        if (ship == null)
        {
            LethalShipSort.Logger.LogWarning("   Couldn't find ship");
            return false;
        }

        if (relativeTo == null)
            relativeTo = ship;
        if (LethalShipSort.Instance.UseRaycast)
            if (
                Physics.Raycast(
                    relativeTo.transform.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    (int)(
                        Layers.Room
                        | Layers.InteractableObject
                        | Layers.Colliders
                        | Layers.Vehicle
                        | Layers.Railing
                        | Layers.PlaceableShipObjects
                    ),
                    QueryTriggerInteraction.Ignore
                )
            )
                position = Randomize(
                    ship.transform.InverseTransformPoint(
                        hitInfo.point + item.itemProperties.verticalOffset * Vector3.up
                    )
                );
            else
            {
                LethalShipSort.Logger.LogWarning("   Raycast unsuccessful");
                return false;
            }
        else
            position = Randomize(position + item.itemProperties.verticalOffset * Vector3.up);

        GameNetworkManager.Instance.localPlayerController.SetObjectAsNoLongerHeld(
            true,
            true,
            position,
            item
        );
        GameNetworkManager.Instance.localPlayerController.ThrowObjectServerRpc(
            item.NetworkObject,
            true,
            true,
            position,
            -1
        );
        return true;
    }

    public static bool MoveItem(GrabbableObject item, Vector3 position, GameObject parentTo)
    {
        LethalShipSort.Logger.LogDebug(
            $">> Moving item {RemoveClone(item.name)} to position {position} in {RemoveClone(parentTo.name)}"
        );
        if (LethalShipSort.Instance.UseRaycast)
            if (
                Physics.Raycast(
                    parentTo.transform.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    (int)(
                        Layers.Room | Layers.InteractableObject | Layers.Colliders | Layers.Vehicle
                    ),
                    QueryTriggerInteraction.Ignore
                )
            )
                position = parentTo.transform.InverseTransformPoint(
                    Randomize(
                        hitInfo.point
                            + item.itemProperties.verticalOffset * Vector3.up
                            - new Vector3(0f, 0.05f, 0f),
                        0.02f
                    )
                );
            else
            {
                LethalShipSort.Logger.LogWarning("   Raycast unsuccessful");
                return false;
            }
        else
            position = Randomize(
                position
                    + item.itemProperties.verticalOffset * Vector3.up
                    - new Vector3(0f, 0.05f, 0f),
                0.02f
            );

        GameNetworkManager.Instance.localPlayerController.SetObjectAsNoLongerHeld(
            true,
            true,
            position,
            item,
            0
        );
        GameNetworkManager.Instance.localPlayerController.ThrowObjectServerRpc(
            item.NetworkObject,
            true,
            true,
            position,
            0
        );
        GameNetworkManager.Instance.localPlayerController.PlaceGrabbableObject(
            parentTo.transform,
            position,
            false,
            item
        );
        GameNetworkManager.Instance.localPlayerController.PlaceObjectServerRpc(
            item.NetworkObject,
            parentTo,
            position,
            false
        );
        return true;
    }

    public static Vector3 Randomize(Vector3 position, float maxDistance = 0.05f)
    {
        if (maxDistance <= 0)
            throw new ArgumentException("Invalid maxDistance (must be positive)");
        Random rng = new();
        return new Vector3(
            position.x + (float)rng.NextDouble() * maxDistance * 2 - maxDistance,
            position.y,
            position.z + (float)rng.NextDouble() * maxDistance * 2 - maxDistance
        );
    }

    public static string GameObjectPath(GameObject gameObject)
    {
        Transform parent = gameObject.transform.parent;
        string path = gameObject.name;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.transform.parent;
        }

        return path;
    }

    [Flags]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal enum Layers
    {
        Default = 1,
        TransparentFX = 2,
        IgnoreRaycast = 4,
        Player = 8,
        Water = 16,
        UI = 32,
        Props = 64,
        HelmetVisor = 128,
        Room = 256,
        InteractableObject = 512,
        Foliage = 1024,
        Colliders = 2048,
        PhysicsObject = 4096,
        Triggers = 8192,
        MapRadar = 16384,
        NavigationSurface = 32768,
        MoldSpore = 65536,
        Anomaly = 131072,
        LineOfSight = 262144,
        Enemies = 524288,
        PlayerRagdoll = 1048576,
        MapHazards = 2097152,
        ScanNode = 4194304,
        EnemiesNotRendered = 8388608,
        MiscLevelGeometry = 16777216,
        Terrain = 33554432,
        PlaceableShipObjects = 67108864,
        PlacementBlocker = 134217728,
        Railing = 268435456,
        DecalStickableSurface = 536870912,
        Vehicle = 1073741824,
    }
}
