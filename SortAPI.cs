using System.Collections.Generic;
using Lua;

namespace LethalShipSort;

public static class SortAPI
{
    public const string ENV_ABOUT = "about";
    public const string ENV_SCRIPT = "script";
    public const string ENV_VERSION_MAJOR = "version_major";
    public const string ENV_VERSION_MINOR = "version_minor";
    public const string ENV_VERSION_PATCH = "version_patch";
    public const string ENV_ARGS = "args";

    public const string ENV_ITEMS = "items";
    public const string ENV_MOON = "moon";
    public const string ENV_DAYS_LEFT = "remaining_days";
    public const string ENV_UNLOCKABLES = "unlockables";

    public const string ITEM_NAME = "name";
    public const string ITEM_TYPE = "type";
    public const string ITEM_SCRAP = "scrap";
    public const string ITEM_LARGE = "large";
    public const string ITEM_ARG = "arg";
    public const string ITEM_VALUE = "value";
    public const string ITEM_TYPE_INDEX = "index";
    public const string ITEM_TYPE_COUNT = "count";

    public const string MOON_ID = "id";
    public const string MOON_NAME = "name";
    public const string MOON_SCENE_NAME = "scene";

    public const string VECTOR3_ZERO = nameof(VECTOR3_ZERO);
    public const string VECTOR3_ONE = nameof(VECTOR3_ONE);
    public const string VECTOR3_DOWN = nameof(VECTOR3_DOWN);
    public const string VECTOR3_UP = nameof(VECTOR3_UP);
    public const string VECTOR3_LEFT = nameof(VECTOR3_LEFT);
    public const string VECTOR3_RIGHT = nameof(VECTOR3_RIGHT);
    public const string VECTOR3_FORWARD = nameof(VECTOR3_FORWARD);
    public const string VECTOR3_BACK = nameof(VECTOR3_BACK);

    public enum UNLOCKABLE // make sure to update readme if changed
    {
        Cruiser = -1,
        OrangeSuit,
        GreenSuit,
        HazardSuit,
        PajamaSuit,
        CozyLights,
        Teleporter,
        Television,
        Cupboard,
        FileCabinet,
        Toilet,
        Shower,
        Lights,
        RecordPlayer,
        Table,
        RomanticTable,
        Bunkbeds,
        _Terminal,
        SignalTranslator,
        LoudHorn,
        InverseTeleporter,
        JackOLantern,
        WelcomeMat,
        Goldfish,
        PlushiePajamaMan,
        PurpleSuit,
        BeeSuit,
        BunnySuit,
        DiscoBall,
        Microwave,
        SofaChair,
        Fridge,
        ClassicPainting,
        ElectricChair,
        DogHouse,
    }

    public enum TRANSFORM // make sure to update readme if changed
    {
        Ship,
        World,
        Cupboard = UNLOCKABLE.Cupboard,
        Cruiser = UNLOCKABLE.Cruiser,

        Fridge = UNLOCKABLE.Fridge,
        Microwave = UNLOCKABLE.Microwave,

        Teleporter = UNLOCKABLE.Teleporter,
        Television = UNLOCKABLE.Television,
        FileCabinet = UNLOCKABLE.FileCabinet,
        Toilet = UNLOCKABLE.Toilet,
        Shower = UNLOCKABLE.Shower,
        RecordPlayer = UNLOCKABLE.RecordPlayer,
        Table = UNLOCKABLE.Table,
        RomanticTable = UNLOCKABLE.RomanticTable,
        Bunkbeds = UNLOCKABLE.Bunkbeds,
        Terminal = UNLOCKABLE._Terminal,
        SignalTranslator = UNLOCKABLE.SignalTranslator,
        LoudHorn = UNLOCKABLE.LoudHorn,
        InverseTeleporter = UNLOCKABLE.InverseTeleporter,
        JackOLantern = UNLOCKABLE.JackOLantern,
        WelcomeMat = UNLOCKABLE.WelcomeMat,
        Goldfish = UNLOCKABLE.Goldfish,
        PlushiePajamaMan = UNLOCKABLE.PlushiePajamaMan,
        DiscoBall = UNLOCKABLE.DiscoBall,
        SofaChair = UNLOCKABLE.SofaChair,
        ClassicPainting = UNLOCKABLE.ClassicPainting,
        ElectricChair = UNLOCKABLE.ElectricChair,
        DogHouse = UNLOCKABLE.DogHouse,
    }

    public enum PARENT // make sure to update readme if changed
    {
        Ship = TRANSFORM.Ship,
        Cupboard = TRANSFORM.Cupboard,
        Cruiser = TRANSFORM.Cruiser,

        Fridge = TRANSFORM.Fridge,
        Microwave = TRANSFORM.Microwave,
    }

    public enum RELATIVE // make sure to update readme if changed
    {
        Parent,
        World = TRANSFORM.World,

        Teleporter = TRANSFORM.Teleporter,
        Television = TRANSFORM.Television,
        FileCabinet = TRANSFORM.FileCabinet,
        Toilet = TRANSFORM.Toilet,
        Shower = TRANSFORM.Shower,
        RecordPlayer = TRANSFORM.RecordPlayer,
        Table = TRANSFORM.Table,
        RomanticTable = TRANSFORM.RomanticTable,
        Bunkbeds = TRANSFORM.Bunkbeds,
        Terminal = TRANSFORM.Terminal,
        SignalTranslator = TRANSFORM.SignalTranslator,
        LoudHorn = TRANSFORM.LoudHorn,
        InverseTeleporter = TRANSFORM.InverseTeleporter,
        JackOLantern = TRANSFORM.JackOLantern,
        WelcomeMat = TRANSFORM.WelcomeMat,
        Goldfish = TRANSFORM.Goldfish,
        PlushiePajamaMan = TRANSFORM.PlushiePajamaMan,
        DiscoBall = TRANSFORM.DiscoBall,
        SofaChair = TRANSFORM.SofaChair,
        ClassicPainting = TRANSFORM.ClassicPainting,
        ElectricChair = TRANSFORM.ElectricChair,
        DogHouse = TRANSFORM.DogHouse,
    }

    public enum ROTATE // make sure to update readme if changed
    {
        Local, // local (relative_to)
        Parent, // local (parent_to)
        World, // global
        None,
    }

    public static string ItemName(GrabbableObject item)
    {
        const string CLONE = "(Clone)";
        var name = item.gameObject.name;
        return name.EndsWith(CLONE) ? name[..^CLONE.Length] : name;
    }

    public static bool ItemLarge(GrabbableObject item)
    {
        return item.itemProperties is { twoHanded: true };
    }

    public static bool ItemScrap(GrabbableObject item)
    {
        return item.itemProperties is not { isScrap: false };
    }

    public static LuaValue ItemArg(GrabbableObject item)
    {
        switch (item)
        {
            case ShotgunItem shotgunItem:
                return shotgunItem.shellsLoaded;

            case StunGrenadeItem stunGrenadeItem:
                return stunGrenadeItem.hasExploded;

            case TetraChemicalItem tetraChemicalItem:
                return tetraChemicalItem.fuel;

            case RadarBoosterItem radarBoosterItem:
                return radarBoosterItem.radarEnabled;

            case JetpackItem jetpackItem:
                return jetpackItem.jetpackBroken;

            case SprayPaintItem sprayPaintItem:
                return sprayPaintItem.sprayCanTank;

            case BeltBagItem beltBagItem:
                return beltBagItem.objectsInBag.Count;

            default:
                return new LuaValue();
        }
    }

    private struct _Item
    {
        public string Name;
        public string Type;
        public bool Large;
        public bool Scrap;

        public LuaValue Arg;
        public int Value;

        public int Index;
    }

    public static LuaTable LuaItems(GrabbableObject[] items)
    {
        var _items = new _Item[items.Length];

        var itemTypes = new Dictionary<string, int>();

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var name = ItemName(item);
            _items[i] = new _Item
            {
                Name = name,
                Type = item.GetType().Name,
                Large = ItemLarge(item),
                Scrap = ItemScrap(item),

                Arg = ItemArg(item),
                Value = item.scrapValue,

                Index = itemTypes.ContainsKey(name) ? ++itemTypes[name] : itemTypes[name] = 1,
            };
        }

        var itemsTable = new LuaTable(items.Length, 0);

        for (var i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            itemsTable[i + 1] = new LuaTable
            {
                [ITEM_NAME] = item.Name,
                [ITEM_TYPE] = item.Type,
                [ITEM_SCRAP] = item.Scrap,
                [ITEM_LARGE] = item.Large,
                [ITEM_ARG] = item.Arg,
                [ITEM_VALUE] = item.Value,
                [ITEM_TYPE_INDEX] = item.Index,
                [ITEM_TYPE_COUNT] = itemTypes[item.Name],
            };
        }

        return itemsTable;
    }

    public static LuaTable LuaMoon(SelectableLevel level)
    {
        return new LuaTable
        {
            [MOON_ID] = level.levelID,
            [MOON_NAME] = level.PlanetName,
            [MOON_SCENE_NAME] = level.sceneName,
        };
    }

    public static LuaTable LuaUnlockables(
        bool cruiser,
        bool lights,
        UnlockablesList unlockablesList
    )
    {
        var unlockablesTable = new LuaTable { [(int)UNLOCKABLE.Cruiser + 1] = cruiser };
        for (var i = 0; i < unlockablesList.unlockables.Count; i++)
        {
            var unlockable = unlockablesList.unlockables[i];
#if DEBUG
            LethalShipSort.Logger.LogDebug($"#{i}: {unlockable.unlockableName}");
#endif
            unlockablesTable[i + 1] =
                (unlockable.alreadyUnlocked || unlockable.hasBeenUnlockedByPlayer)
                && !unlockable.inStorage;
        }

        unlockablesTable[(int)UNLOCKABLE.Lights + 1] = lights;

        return unlockablesTable;
    }

    public static LuaTable LuaTable(LuaValue[] list)
    {
        var table = new LuaTable();
        for (var i = 1; i <= list.Length; i++)
            table[i] = list[i];
        return table;
    }

    public static TRANSFORM ToTransform(PARENT parent, RELATIVE relative)
    {
        return relative switch
        {
            RELATIVE.Parent => (TRANSFORM)parent,
            _ => (TRANSFORM)relative,
        };
    }
}
