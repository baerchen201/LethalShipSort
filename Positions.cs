using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace LethalShipSort;

public static class Positions
{
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

    public static Vector3 GetPosition() => GetPosition(null!, false);

    public static Vector3 GetPosition(string name, bool twoHanded = false) =>
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Utils.RemoveClone(name ?? "") switch
        {
            "SoccerBall" => new Vector3(-6.8f, 4.4f, -7.75f),
            "LungApparatusTurnedOff" or "LungApparatus" => new Vector3(-6.8f, 4.4f, -6.65f),
            "RedLocustHive" => new Vector3(-6.8f, 4.4f, -5.65f),
            "WhoopieCushion" => new Vector3(9f, 2f, -8.25f),
            "ShotgunItem" => new Vector3(8.75f, 2f, -5.5f),
            "ZeddogPlushie" => new Vector3(9f, 1.21f, -5.55f),
            _ => twoHanded ? new Vector3(-4.5f, 3f, -5.25f) : new Vector3(-2.25f, 2f, -5.25f),
        };

    private const float FLOOR_ABOVE = 3.2f;
    private const float FLOOR_TOP = 2.4f;
    private const float FLOOR_MIDDLE_1 = 2f;
    private const float FLOOR_MIDDLE_2 = 1.5f;
    private const float FLOOR_BOTTOM = 0.5f;

    public static Vector3 GetToolPosition(string name) =>
        Utils.RemoveClone(name) switch
        {
            "ShovelItem" => new Vector3(-1.5f, 0.3f, FLOOR_MIDDLE_2),
            "Key" => new Vector3(-0.3f, 0.6f, FLOOR_MIDDLE_2),
            "ShotgunShell" => new Vector3(-0.3f, 0.6f, FLOOR_MIDDLE_1),
            "Boombox" => new Vector3(-0.3f, 0.5f, FLOOR_ABOVE),
            "JetpackItem" => new Vector3(-0.3f, 0.2f, FLOOR_BOTTOM),

            "WeedKillerItem" => new Vector3(-2.05f, 0.5f, FLOOR_MIDDLE_1),
            "SprayPaintItem" => new Vector3(-1.7f, 0.5f, FLOOR_MIDDLE_1),
            "BBFlashlight" => new Vector3(-1.3f, 0.2f, FLOOR_MIDDLE_1),
            "FlashlightItem" => new Vector3(-1.3f, 0.65f, FLOOR_MIDDLE_1),
            "StunGrenade" => new Vector3(-1.2f, 0.5f, FLOOR_MIDDLE_1),
            "TZPChemical" => new Vector3(-0.55f, 0.2f, FLOOR_MIDDLE_1),

            "LockPickerItem" => new Vector3(-2f, 0.5f, FLOOR_TOP),
            "WalkieTalkie" => new Vector3(-1.4f, 0.6f, FLOOR_TOP),
            "PatcherGunItem" => new Vector3(-1.1f, 0.6f, FLOOR_TOP),
            "BeltBagItem" => new Vector3(-0.35f, 0.5f, FLOOR_TOP - 0.1f),
            _ => new Vector3(-2f, 0.6f, FLOOR_BOTTOM),
        };

    public static Vector3 GetPosition(GrabbableObject item) =>
        item.itemProperties.isScrap
            ? GetPosition(item.name, item.itemProperties.twoHanded)
            : GetToolPosition(item.name);

    [Obsolete("Use GetPosition() instead")]
    public static readonly (float, float) FALLBACK_POSITION = (2.86f, -6.08f);

    [Obsolete("Use GetPosition(item.name, item.itemProperties.twoHanded) instead")]
    public static readonly Dictionary<string, (float, float)> NAMED_POSITIONS = new() // Incomplete
    {
        ["Hairdryer"] = (-1.96f, -5.26f),
        ["Hairbrush"] = (1.75f, -5.96f),
        ["EasterEgg"] = (-6.32f, -7.1f),
        ["Mug"] = (-2.45f, -6.87f),
        ["Dentures"] = (-3.14f, -5.85f),
        ["FancyLamp"] = (1.62f, -6.73f),
        ["ComedyMask"] = (-3.92f, -7f),
        ["FancyRing"] = (-3.33f, -6.91f),
        ["TragedyMask"] = (-4.08f, -6.46f),
        ["BinFullOfBottles"] = (-5.03f, -7.11f),
        ["ToyCube"] = (-3.48f, -4.93f),
        ["FancyGlass"] = (-5.9f, -4.88f),
        ["FishTestProp"] = (-6.34f, -7.7f),
        ["PerfumeBottle"] = (-1.25f, -8.46f),
        ["Painting"] = (1.29f, -8.36f),
        ["Airhorn"] = (-0.06f, -5.52f),
        ["RedSodaCan"] = (-1.88f, -6.16f),
        ["RubberDucky"] = (8.38f, -6.48f),
        ["MagnifyingGlass"] = (-3.93f, -7.45f),
        ["Toothpaste"] = (-4.95f, -7.78f),
        ["Candy"] = (-3.57f, -8.46f),
        ["RedLocustHive"] = (6.32f, -5.13f),
        ["TeaKettle"] = (-4.98f, -6.25f),
        ["HandBell"] = (-4.48f, -5.54f),
        ["RobotToy"] = (-0.25f, -8.45f),
        ["Clownhorn"] = (-0.06f, -5.52f),
        ["OldPhone"] = (-1.8f, -7.05f),
        ["LaserPointer"] = (-6.34f, -6.85f),
        ["Magic7Ball"] = (-6.2f, -6.11f),

        ["StopSign"] = (2.86f, -6.08f),
        ["YieldSign"] = (2.86f, -6.08f),
        ["CookieMoldPan"] = (2.86f, -6.08f),
        ["DiyFlashbang"] = (2.86f, -6.08f),
        ["PillBottle"] = (2.86f, -6.08f),
        ["Dustpan"] = (2.86f, -6.08f),
        ["SteeringWheel"] = (2.86f, -6.08f),
        ["Remote"] = (2.86f, -6.08f),
        ["ChemicalJug"] = (2.86f, -6.08f),
        ["Flask"] = (2.86f, -6.08f),
        ["EnginePart"] = (2.86f, -6.08f),
        ["EggBeater"] = (2.86f, -6.08f),
        ["BigBolt"] = (2.86f, -6.08f),
        ["MetalSheet"] = (2.86f, -6.08f),
        ["WhoopieCushion"] = (8.46f, -7.7f),
        ["Cog"] = (2.86f, -6.08f),
    };
}

[Obsolete]
internal static class ItemList
{
    internal static readonly Dictionary<string, Vector3> GoodItems = new Dictionary<string, Vector3>
    {
        { "Hairdryer", new Vector3(-0.69f, 0.35f, -12.76f) },
        { "Hairbrush", new Vector3(3.02f, 0.38f, -13.46f) },
        { "EasterEgg", new Vector3(-5.05f, 0.35f, -14.6f) },
        { "Mug", new Vector3(-1.18f, 0.35f, -14.37f) },
        { "Dentures", new Vector3(-1.87f, 0.36f, -13.35f) },
        { "FancyLamp", new Vector3(2.89f, 0.29f, -14.23f) },
        { "ComedyMask", new Vector3(-2.65f, 0.35f, -14.5f) },
        { "FancyRing", new Vector3(-2.06f, 0.35f, -14.41f) },
        { "TragedyMask", new Vector3(-2.81f, 0.35f, -13.96f) },
        { "BinFullOfBottles", new Vector3(-3.76f, 0.35f, -14.61f) },
        { "ToyCube", new Vector3(-2.21f, 0.35f, -12.43f) },
        { "FancyGlass", new Vector3(-4.63f, 0.35f, -12.38f) },
        { "FishTestProp", new Vector3(-5.07f, 0.35f, -15.2f) },
        { "PerfumeBottle", new Vector3(0.02f, 0.35f, -15.96f) },
        { "Painting", new Vector3(2.56f, 0.29f, -15.86f) },
        { "Airhorn", new Vector3(1.21f, 0.35f, -13.02f) },
        { "RedSodaCan", new Vector3(-0.61f, 0.35f, -13.66f) },
        { "RubberDucky", new Vector3(9.65f, 1.73f, -13.98f) },
        { "MagnifyingGlass", new Vector3(-2.66f, 0.38f, -14.95f) },
        { "Toothpaste", new Vector3(-3.68f, 0.35f, -15.28f) },
        { "Candy", new Vector3(-2.3f, 0.35f, -15.96f) },
        { "RedLocustHive", new Vector3(7.59f, 0.29f, -12.63f) },
        { "TeaKettle", new Vector3(-3.71f, 0.35f, -13.75f) },
        { "HandBell", new Vector3(-3.21f, 0.35f, -13.04f) },
        { "RobotToy", new Vector3(1.02f, 0.29f, -15.95f) },
        { "Clownhorn", new Vector3(1.21f, 0.35f, -13.02f) },
        { "OldPhone", new Vector3(-0.53f, 0.35f, -14.55f) },
        { "LaserPointer", new Vector3(-5.07f, 0.35f, -14.35f) },
        { "Magic7Ball", new Vector3(-4.93f, 0.37f, -13.61f) },
    };

    internal static readonly Dictionary<string, Vector3> BadItems = new Dictionary<string, Vector3>
    {
        { "StopSign", new Vector3(4.13f, 0.37f, -13.58f) },
        { "YieldSign", new Vector3(4.13f, 0.37f, -13.58f) },
        { "CookieMoldPan", new Vector3(4.13f, 0.37f, -13.58f) },
        { "DiyFlashbang", new Vector3(4.13f, 0.37f, -13.58f) },
        { "PillBottle", new Vector3(4.13f, 0.37f, -13.58f) },
        { "Dustpan", new Vector3(4.13f, 0.37f, -13.58f) },
        { "SteeringWheel", new Vector3(4.13f, 0.37f, -13.58f) },
        { "Remote", new Vector3(4.13f, 0.37f, -13.58f) },
        { "ChemicalJug", new Vector3(4.13f, 0.37f, -13.58f) },
        { "Flask", new Vector3(4.13f, 0.37f, -13.58f) },
        { "EnginePart", new Vector3(4.13f, 0.37f, -13.58f) },
        { "EggBeater", new Vector3(4.13f, 0.37f, -13.58f) },
        { "BigBolt", new Vector3(4.13f, 0.37f, -13.58f) },
        { "MetalSheet", new Vector3(4.13f, 0.37f, -13.58f) },
        { "WhoopieCushion", new Vector3(9.73f, 1.74f, -15.2f) },
        { "Cog", new Vector3(4.13f, 0.37f, -13.58f) },
    };
}
