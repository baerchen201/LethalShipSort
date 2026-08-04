using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChatCommandAPI.Utils;
using GameNetcodeStuff;
using HarmonyLib;
using LethalShipSort.Commands;
using LethalShipSort.LuaObjects;
using Lua;
using Lua.Standard;
using UnityEngine;
using Vector3 = LethalShipSort.LuaObjects.Vector3;

namespace LethalShipSort;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LethalShipSort : BaseUnityPlugin
{
    internal const int LAYER_MASK = 268437761;
    private const float VERTICAL_OFFSET = 0.02f;

    public static LethalShipSort Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony Harmony = null!;

    private ConfigEntry<string> scriptPath = null!;
    public string ScriptPath =>
        Path.IsPathRooted(scriptPath.Value)
            ? scriptPath.Value
            : Path.Join(Path.GetDirectoryName(Config.ConfigFilePath), scriptPath.Value);

    private void Awake()
    {
        const string CONFIG_SECTION_GENERAL = "General";

        Logger = base.Logger;
        Instance = this;

        scriptPath = Config.Bind(
            CONFIG_SECTION_GENERAL,
            nameof(ScriptPath),
            "sort.lua",
            "The item sorting script to use (absolute path or relative to this config file)"
        );

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Logger.LogDebug("Patching...");
        Harmony.PatchAll();
        Logger.LogDebug("Finished patching!");

        _ = new SortCommand();
#if DEBUG
        _ = new SortHelperCommand();
#endif

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    public static GrabbableObject[] FilterItems(
        IEnumerable<GrabbableObject> items,
        PlayerControllerB localPlayer
    )
    {
        return items.Where(i => FilterItem(i, localPlayer)).ToArray();
    }

    public static bool FilterItem(GrabbableObject item, PlayerControllerB localPlayer)
    {
        return (item.playerHeldBy != null && item.playerHeldBy == localPlayer)
            || item is { isHeld: false, isPocketed: false }; // TODO: figure out something for belt bag (potentially add a patch that sets isPocketed on pickup/drop)
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Sort(
        GrabbableObject[] items,
        PlayerControllerB player,
        SelectableLevel currentLevel,
        int daysUntilDeadline,
        bool cruiser,
        bool lights,
        UnlockablesList unlockablesList,
        uint skipped = 0
    )
    {
        return Sort(
            items,
            player,
            currentLevel,
            daysUntilDeadline,
            cruiser,
            lights,
            unlockablesList,
            ScriptPath,
            skipped
        );
    }

    public static string Sort(
        GrabbableObject[] items,
        PlayerControllerB player,
        SelectableLevel currentLevel,
        int daysUntilDeadline,
        bool cruiser,
        bool lights,
        UnlockablesList unlockablesList,
        string scriptPath,
        uint skipped = 0
    )
    {
        var lua = LuaState.Create();

        lua.OpenBasicLibrary();
        lua.OpenStringLibrary();
        lua.OpenTableLibrary();
        lua.OpenMathLibrary();
        lua.OpenBitwiseLibrary();

        lua.Environment[nameof(print)] = new LuaFunction(print);
        lua.Environment[nameof(raycast)] = new LuaFunction(raycast);
        lua.Environment[nameof(transform)] = new LuaFunction(transform);
        lua.Environment[nameof(Vector3)] = new Vector3(UnityEngine.Vector3.zero);
        lua.Environment[nameof(ItemPos)] = new ItemPos(new Vector3(UnityEngine.Vector3.zero));
        lua.Environment[nameof(RaycastPos)] = new RaycastPos(new Vector3(UnityEngine.Vector3.zero));

        AddEnum(lua, typeof(SortAPI.UNLOCKABLE), true);
        AddEnum(lua, typeof(SortAPI.PARENT), false);
        AddEnum(lua, typeof(SortAPI.RELATIVE), false);
        AddEnum(lua, typeof(SortAPI.ROTATE), false);
        AddEnum(lua, typeof(SortAPI.TRANSFORM), false);

        lua.Environment[SortAPI.VECTOR3_DOWN] = new Vector3(UnityEngine.Vector3.down);
        lua.Environment[SortAPI.VECTOR3_UP] = new Vector3(UnityEngine.Vector3.up);
        lua.Environment[SortAPI.VECTOR3_LEFT] = new Vector3(UnityEngine.Vector3.left);
        lua.Environment[SortAPI.VECTOR3_RIGHT] = new Vector3(UnityEngine.Vector3.right);
        lua.Environment[SortAPI.VECTOR3_FORWARD] = new Vector3(UnityEngine.Vector3.forward);
        lua.Environment[SortAPI.VECTOR3_BACK] = new Vector3(UnityEngine.Vector3.back);

        lua.Environment[SortAPI.ENV_ITEMS] = SortAPI.LuaItems(items);
        lua.Environment[SortAPI.ENV_MOON] = SortAPI.LuaMoon(currentLevel);
        lua.Environment[SortAPI.ENV_DAYS_LEFT] = daysUntilDeadline;
        lua.Environment[SortAPI.ENV_UNLOCKABLES] = SortAPI.LuaUnlockables(
            cruiser,
            lights,
            unlockablesList
        );

        var cancellationToken = new CancellationTokenSource();
        var task = lua.DoFileAsync(scriptPath, cancellationToken.Token);
        try
        {
            if (
                !task.AsTask()
                    .Wait(
                        new TimeSpan(
                            5 * 1000 * 10000 // 5 seconds
                        )
                    )
            )
            {
#if DEBUG
                Chat.PrintWarning("Script execution passed release build timeout");
                if (
                    !task.AsTask()
                        .Wait(
                            new TimeSpan(
                                25 * 1000 * 10000 // 25 seconds
                            )
                        )
                )
#endif
                {
                    cancellationToken.Cancel();
                    throw new TimeoutException();
                }
            }
        }
        catch (AggregateException e)
        {
            if (e.InnerExceptions.Count == 1)
                throw e.InnerException!;
            throw;
        }

        var results = task.Result!;

        uint sorted = 0;
        uint failed = 0;

        switch (results.Length)
        {
            case 0:
                return "no items sorted";
            case 1:
                if (!results[0].TryRead<LuaTable>(out var result))
                    throw new ArgumentException(
                        $"expected table return value, got {results[0].TypeToString()}"
                    );

                for (var i = 1; i <= items.Length; i++)
                {
                    if (!result.TryGetValue(i, out var value))
                    {
#if DEBUG
                        Logger.LogDebug($"{i}: null");
#endif
                        skipped++;
                        continue;
                    }

                    if (!value.TryRead<ItemPos>(out var pos))
                        if (!value.TryRead<Vector3>(out var vector3))
                        {
                            Logger.LogError(
                                $"expected {nameof(ItemPos)} or {nameof(Vector3)} at #{i}, got {value.TypeToString()}"
                            );
                            failed++;
                            continue;
                        }
                        else
                        {
                            pos = new ItemPos(vector3);
                        }
#if DEBUG
                    Logger.LogDebug($"{i}: {pos}");
#endif
                    var item = items[i - 1];
                    if (sorted++ <= 0)
                        player.DropAllHeldItemsAndSyncNonexact();

                    var parent = GetTransform((SortAPI.TRANSFORM)pos.ParentTo, out _);
                    var relative = GetTransform(
                        SortAPI.ToTransform(pos.ParentTo, pos.RelativeTo),
                        out _
                    );

                    var position = parent.InverseTransformPoint(
                        (
                            pos.RelativeTo != SortAPI.RELATIVE.Parent
                                ? relative.TransformPoint(pos.Position)
                                : parent.TransformPoint(pos.Position)
                        )
                            + (
                                item.itemProperties != null
                                    ? (item.itemProperties.verticalOffset - VERTICAL_OFFSET)
                                        * UnityEngine.Vector3.up
                                    : VERTICAL_OFFSET * UnityEngine.Vector3.down
                            )
                    );
                    var floorYRot = pos.RotationMode switch
                    {
                        SortAPI.ROTATE.Local => (
                            pos.Rotation + Mathf.RoundToInt(relative.eulerAngles.y)
                        ) % 360,
                        SortAPI.ROTATE.Parent => (
                            pos.Rotation + Mathf.RoundToInt(parent.eulerAngles.y)
                        ) % 360,
                        SortAPI.ROTATE.World => pos.Rotation,
                        _ => -1,
                    };

#if DEBUG
                    Logger.LogDebug($">> {position} rot {floorYRot}");
#endif
                    player.SetObjectAsNoLongerHeld(true, true, position, item, floorYRot);
                    player.ThrowObjectServerRpc(
                        item.NetworkObject,
                        true,
                        true,
                        position,
                        floorYRot
                    );

                    player.PlaceGrabbableObject(parent, position, false, item);
                    player.PlaceObjectServerRpc(
                        item.NetworkObject,
                        parent.gameObject,
                        position,
                        false
                    );
                }

                return $"sorted {sorted}, failed {failed}, skipped {skipped}";
            default:
                throw new ArgumentException("expected single return value, got multiple");
        }
    }

    private static ValueTask<int> print(
        LuaFunctionExecutionContext ctx,
        CancellationToken cancellationToken
    )
    {
        var sb = new StringBuilder();
        foreach (var arg in ctx.Arguments)
            sb.Append($" {arg.ToString()}");
        Logger.LogInfo($"(Script){sb}");
        return new ValueTask<int>(0);
    }

    private static ValueTask<int> raycast(
        LuaFunctionExecutionContext ctx,
        CancellationToken cancellationToken
    )
    {
        RaycastPos pos;

        switch (ctx.ArgumentCount)
        {
            case 1:
                var arg = ctx.Arguments[0];
                if (arg.TryRead(out pos))
                    break;
                if (!arg.TryRead<Vector3>(out var _position))
                    throw new ArgumentException();
                pos = new RaycastPos(_position);
                break;
            case 2:
                if (!ctx.Arguments[0].TryRead(out _position!))
                    throw new ArgumentException();

                arg = ctx.Arguments[1];
                if (arg.TryRead<Vector3>(out var _direction))
                {
                    pos = new RaycastPos(_position, _direction);
                }
                else if (arg.TryRead<int>(out var _relative_to))
                {
                    if (!Enum.IsDefined(typeof(SortAPI.TRANSFORM), _relative_to))
                        throw new ArgumentOutOfRangeException();
                    pos = new RaycastPos(_position, _relative_to: (SortAPI.TRANSFORM)_relative_to);
                }
                else
                {
                    throw new ArgumentException();
                }

                break;
            default:
                throw new ArgumentException();
        }
#if DEBUG
        Logger.LogDebug($"(Script requested raycast) from {pos}");
#endif
        var transform = GetTransform(
            pos.RelativeTo,
            out var transformDirection // i have to do this horribleness cause the game is gay and half the objects in the game rotate the vector weirdly
        );
        var origin = transform.TransformPoint(pos.Position);
        var direction = transformDirection
            ? transform.TransformDirection(pos.Direction)
            : pos.Direction;
#if DEBUG
        Logger.LogDebug($">> {origin} towards {direction}");
#endif

        if (
            Physics.Raycast(
                origin,
                direction,
                out var hitInfo,
                80f,
                LAYER_MASK, // Copied this straight from GrabbableObject.GetItemFloorPosition
                QueryTriggerInteraction.Ignore
            )
        )
        {
#if DEBUG
            Logger.LogDebug($"<< Hit {hitInfo.point} ({hitInfo.collider})");
#endif
            return new ValueTask<int>(
                ctx.Return(
                    new Vector3(
                        transform.InverseTransformPoint(
                            hitInfo.point + VERTICAL_OFFSET * UnityEngine.Vector3.up
                        )
                    )
                )
            );
        }

#if DEBUG
        Logger.LogDebug("<< No hit");
#endif
        return new ValueTask<int>(ctx.Return(new LuaValue()));
    }

    private static new ValueTask<int> transform(
        LuaFunctionExecutionContext ctx,
        CancellationToken cancellationToken
    )
    {
        if (ctx.ArgumentCount != 3)
            throw new ArgumentException();

        if (
            !ctx.Arguments[0].TryRead<Vector3>(out var position)
            || !ctx.Arguments[1].TryRead<int>(out var from)
            || !ctx.Arguments[2].TryRead<int>(out var to)
        )
            throw new ArgumentException();

        if (
            !Enum.IsDefined(typeof(SortAPI.TRANSFORM), from)
            || !Enum.IsDefined(typeof(SortAPI.TRANSFORM), to)
        )
            throw new ArgumentOutOfRangeException();

#if DEBUG
        Logger.LogDebug(
            $"(Script requested transform) {position} from {(SortAPI.TRANSFORM)from} to {(SortAPI.TRANSFORM)to}"
        );
#endif
        var fromTransform = GetTransform((SortAPI.TRANSFORM)from, out _);
        var toTransform = GetTransform((SortAPI.TRANSFORM)to, out _);

        return new ValueTask<int>(
            ctx.Return(
                new Vector3(
                    toTransform.InverseTransformPoint(fromTransform.TransformPoint(position))
                )
            )
        );
    }

    private static void AddEnum(LuaState lua, Type enumType, bool indexEnum)
    {
        var typeName = enumType.Name.ToUpperInvariant();
        var sb = new StringBuilder();
        foreach (var value in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, value)!;
            if (name.StartsWith('_'))
                continue;
            sb.Append(char.ToUpperInvariant(name[0]));
            foreach (var c in name[1..])
            {
                if (char.IsUpper(c))
                    sb.Append('_');
                sb.Append(char.ToUpperInvariant(c));
            }

            lua.Environment[$"{typeName}_{sb}"] = (int)value + (indexEnum ? 1 : 0);
            sb.Clear();
        }
    }

    internal static Transform GetTransform(SortAPI.TRANSFORM transform, out bool transformDirection)
    {
        transformDirection = false;
        switch (transform)
        {
            case SortAPI.TRANSFORM.Ship:
                transformDirection = true;
                return GameObject.Find("/Environment/HangarShip").transform;
            case SortAPI.TRANSFORM.World:
                return GameObject.Find("/Environment").transform;

            case SortAPI.TRANSFORM.Cruiser:
                transformDirection = true;
                return FindFirstObjectByType<VehicleController>().transform;
            case SortAPI.TRANSFORM.Teleporter:
                return GameObject.Find("/Teleporter(Clone)").transform;
            case SortAPI.TRANSFORM.Television:
                return GameObject.Find("/TelevisionContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Cupboard:
                return GameObject.Find("/Environment/HangarShip/StorageCloset").transform;
            case SortAPI.TRANSFORM.FileCabinet:
                return GameObject.Find("/Environment/HangarShip/FileCabinet").transform;
            case SortAPI.TRANSFORM.Toilet:
                return GameObject.Find("/Toilet(Clone)").transform;
            case SortAPI.TRANSFORM.Shower:
                return GameObject.Find("/Shower(Clone)").transform;
            case SortAPI.TRANSFORM.RecordPlayer:
                return GameObject.Find("/RecordPlayerContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Table:
                return GameObject.Find("/NormalTableContainer(Clone)").transform;
            case SortAPI.TRANSFORM.RomanticTable:
                return GameObject.Find("/RomanticTableContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Bunkbeds:
                return GameObject.Find("/Environment/HangarShip/Bunkbeds").transform;
            case SortAPI.TRANSFORM.Terminal:
                return GameObject.Find("/Environment/HangarShip/Terminal").transform;
            case SortAPI.TRANSFORM.SignalTranslator:
                return GameObject.Find("/SignalTranslator(Clone)").transform;
            case SortAPI.TRANSFORM.LoudHorn:
                return GameObject.Find("/ShipHorn(Clone)").transform;
            case SortAPI.TRANSFORM.InverseTeleporter:
                return GameObject.Find("/InverseTeleporter(Clone)").transform;
            case SortAPI.TRANSFORM.JackOLantern:
                return GameObject.Find("/PumpkinUnlockableContainer(Clone)").transform;
            case SortAPI.TRANSFORM.WelcomeMat:
                return GameObject.Find("/WelcomeMatContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Goldfish:
                return GameObject.Find("/FishBowlContainer(Clone)").transform;
            case SortAPI.TRANSFORM.PlushiePajamaMan:
                return GameObject.Find("/PlushiePJManContainer(Clone)").transform;
            case SortAPI.TRANSFORM.DiscoBall:
                return GameObject.Find("/DiscoBallContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Microwave:
                return GameObject.Find("/MicrowaveContainer(Clone)").transform;
            case SortAPI.TRANSFORM.SofaChair:
                return GameObject.Find("/SofaChairContainer(Clone)").transform;
            case SortAPI.TRANSFORM.Fridge:
                return GameObject.Find("/FridgeContainer(Clone)").transform;
            case SortAPI.TRANSFORM.ClassicPainting:
                return GameObject.Find("/ClassicPaintingContainer(Clone)").transform;
            case SortAPI.TRANSFORM.ElectricChair:
                return GameObject.Find("/ElectricChair(Clone)").transform;
            case SortAPI.TRANSFORM.DogHouse:
                return GameObject.Find("/DogHouse(Clone)").transform;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
