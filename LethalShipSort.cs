using System;
using System.Collections.Generic;
using System.Globalization;
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

    private static readonly Version VERSION = new(MyPluginInfo.PLUGIN_VERSION);

    private ConfigEntry<string> scriptPath = null!;
    public string ScriptPath =>
        Path.IsPathRooted(scriptPath.Value)
            ? scriptPath.Value
            : Path.Join(Path.GetDirectoryName(Config.ConfigFilePath), scriptPath.Value);

    private ConfigEntry<uint> timeout = null!;
    public int Timeout => (int)Math.Min(int.MaxValue, timeout.Value);

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
        timeout = Config.Bind(
            CONFIG_SECTION_GENERAL,
            nameof(Timeout),
            5U,
            "The maximum execution time for the sorting script in seconds (prevents freezing, 0 to disable [NOT RECOMMENDED])"
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

        lua.Environment[SortAPI.ENV_ABOUT] =
            $"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION}";
        lua.Environment[SortAPI.ENV_SCRIPT] = Path.GetFileName(scriptPath);
        lua.Environment[SortAPI.ENV_VERSION_MAJOR] = VERSION.Major;
        lua.Environment[SortAPI.ENV_VERSION_MINOR] = VERSION.Minor;
        lua.Environment[SortAPI.ENV_VERSION_PATCH] = VERSION.Build;

        lua.Environment[nameof(expect_version)] = new LuaFunction(expect_version);

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

        lua.Environment[SortAPI.VECTOR3_ZERO] = new Vector3(UnityEngine.Vector3.zero);
        lua.Environment[SortAPI.VECTOR3_ONE] = new Vector3(UnityEngine.Vector3.one);
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

        var startTime = DateTime.UtcNow;
        var cancellationToken = new CancellationTokenSource();
        var task = Task.Run(async () => await lua.DoFileAsync(scriptPath, cancellationToken.Token));
        try
        {
            var timeout = Instance.Timeout;
#if DEBUG
            var defaultTimeout = (int)(uint)Instance.timeout.DefaultValue; // no limiting here, the default should always be small enough to fit in an int
            if (timeout <= defaultTimeout) // and it should always be above 0
                goto timeout;

            if (!task.Wait(new TimeSpan(0, 0, defaultTimeout)))
            {
                if (!task.Wait(new TimeSpan(0, 0, timeout - defaultTimeout)))
                {
                    cancellationToken.Cancel();
                    throw new TimeoutException();
                }
                Chat.PrintWarning(
                    $"Script execution time passed default timeout ({defaultTimeout} seconds), consider optimizing it"
                );
            }
            goto skipTimeout;

            timeout:
#endif
            if (timeout > 0)
            {
                if (!task.Wait(new TimeSpan(0, 0, timeout)))
                {
                    cancellationToken.Cancel();
                    throw new TimeoutException();
                }
            }
            else
                task.Wait();
#if DEBUG
            skipTimeout:
            ;
#endif
        }
        catch (AggregateException e)
        {
            if (e.InnerExceptions.Count == 1)
                throw e.InnerException!;
            throw;
        }

        var scriptEndTime = DateTime.UtcNow;
        var results = task.Result!;

        uint sorted = 0;
        uint failed = 0;

        switch (results.Length)
        {
            case 0:
                return "no items sorted"; // TODO: throw exception? should this be enforced?
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
                    if (parent == null)
                    {
                        Logger.LogError($"null parent at #{i}: {pos.ParentTo}");
                        failed++;
                        continue;
                    }

                    var relative = GetTransform(
                        SortAPI.ToTransform(pos.ParentTo, pos.RelativeTo),
                        out _
                    );
                    if (relative == null)
                    {
                        Logger.LogError($"null relative at #{i}: {pos.RelativeTo}");
                        failed++;
                        continue;
                    }

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

                    item.rotateObject = pos.ParentTo == SortAPI.PARENT.Microwave;
                    // && parent.gameObject.GetComponentInChildren<MicrowaveItem>()?.whirringAudio.isPlaying is true
                    // ^^ this doesn't work until you open and close microwave because this game is a piece of shit
                }

                var sortEndTime = DateTime.UtcNow;
                Logger.LogDebug(
                    $"{nameof(startTime)}:{startTime.ToString("o", CultureInfo.InvariantCulture)} {nameof(scriptEndTime)}:{scriptEndTime.ToString("o", CultureInfo.InvariantCulture)} {nameof(sortEndTime)}:{sortEndTime.ToString("o", CultureInfo.InvariantCulture)}"
                );
                Logger.LogInfo(
                    $"Script execution took {(scriptEndTime - startTime).ToReadableString()}, sorting took {(sortEndTime - scriptEndTime).ToReadableString()}"
                );

                return $"Sorted {sorted}/{items.Length} in {(sortEndTime - startTime).ToReadableString()}"; // TODO: include failed
            default:
                throw new ArgumentException("expected single return value, got multiple");
        }
    }

    private static ValueTask<int> expect_version(
        LuaFunctionExecutionContext ctx,
        CancellationToken cancellationToken
    )
    {
        switch (ctx.ArgumentCount)
        {
            case 1:
                var major = ctx.GetArgument<int>(0);
                if (major < 0)
                    throw new ArgumentOutOfRangeException();
                if (VERSION.Major > major)
                    Chat.PrintWarning(
                        $"Version conflict (Script is outdated: expected v{major}, currently v{VERSION.Major})"
                    );
                else if (VERSION.Major < major)
                    Chat.PrintWarning(
                        $"Version conflict (Mod is outdated: expected v{major}, currently v{VERSION.Major})"
                    );
                break;

            case 2:
                major = ctx.GetArgument<int>(0);
                var minMinor = ctx.GetArgument<int>(1);
                if (major < 0 || minMinor < 0)
                    throw new ArgumentOutOfRangeException();
                if (VERSION.Major < major)
                    Chat.PrintWarning(
                        $"Version conflict (Mod is outdated: expected v{major}, currently v{VERSION.Major})"
                    );
                else if (VERSION.Major > major)
                    Chat.PrintWarning(
                        $"Version conflict (Script is outdated: expected v{major}, currently v{VERSION.Major})"
                    );
                else if (VERSION.Minor < minMinor)
                    Chat.PrintWarning(
                        $"Version conflict (Mod is outdated: expected at least v{major}.{minMinor}, currently v{VERSION.Major}.{VERSION.Minor})"
                    );
                break;
            default:
                throw new ArgumentException();
        }

        ctx.State.Environment[nameof(expect_version)] = new LuaValue();
        return new ValueTask<int>(0);
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
        if (transform == null)
        {
            Logger.LogError(
                $"null relative for raycast: {pos.RelativeTo}\n{ctx.State.GetTraceback()}"
            );
            goto nohit;
        }
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

        nohit:
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
        if (fromTransform == null)
        {
            Logger.LogError(
                $"null from for transform: {(SortAPI.TRANSFORM)from}\n{ctx.State.GetTraceback()}"
            );
            return new ValueTask<int>(ctx.Return(new LuaValue()));
        }

        var toTransform = GetTransform((SortAPI.TRANSFORM)to, out _);
        if (toTransform == null)
        {
            Logger.LogError(
                $"null to for transform: {(SortAPI.TRANSFORM)to}\n{ctx.State.GetTraceback()}"
            );
            return new ValueTask<int>(ctx.Return(new LuaValue()));
        }

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

    internal static Transform? GetTransform(
        SortAPI.TRANSFORM transform,
        out bool transformDirection
    )
    {
        transformDirection = false;
        switch (transform)
        {
            case SortAPI.TRANSFORM.Ship:
                transformDirection = true;
                return GameObject.Find("/Environment/HangarShip")
#if DEBUG
                ?
#endif
                .transform;
            case SortAPI.TRANSFORM.World:
                return GameObject.Find("/Environment")
#if DEBUG
                ?
#endif
                .transform;

            case SortAPI.TRANSFORM.Cruiser:
                transformDirection = true;
                return FindFirstObjectByType<VehicleController>()?.transform;
            case SortAPI.TRANSFORM.Teleporter:
                return GameObject.Find("/Teleporter(Clone)")?.transform;
            case SortAPI.TRANSFORM.Television:
                return GameObject.Find("/TelevisionContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Cupboard:
                return GameObject.Find("/Environment/HangarShip/StorageCloset")
#if DEBUG
                ?
#endif
                .transform;
            case SortAPI.TRANSFORM.FileCabinet:
                return GameObject.Find("/Environment/HangarShip/FileCabinet")
#if DEBUG
                ?
#endif
                .transform;
            case SortAPI.TRANSFORM.Toilet:
                return GameObject.Find("/Toilet(Clone)")?.transform;
            case SortAPI.TRANSFORM.Shower:
                return GameObject.Find("/Shower(Clone)")?.transform;
            case SortAPI.TRANSFORM.RecordPlayer:
                return GameObject.Find("/RecordPlayerContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Table:
                return GameObject.Find("/NormalTableContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.RomanticTable:
                return GameObject.Find("/RomanticTableContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Bunkbeds:
                return GameObject.Find("/Environment/HangarShip/Bunkbeds")
#if DEBUG
                ?
#endif
                .transform;
            case SortAPI.TRANSFORM.Terminal:
                return GameObject.Find("/Environment/HangarShip/Terminal")
#if DEBUG
                ?
#endif
                .transform;
            case SortAPI.TRANSFORM.SignalTranslator:
                return GameObject.Find("/SignalTranslator(Clone)")?.transform;
            case SortAPI.TRANSFORM.LoudHorn:
                return GameObject.Find("/ShipHorn(Clone)")?.transform;
            case SortAPI.TRANSFORM.InverseTeleporter:
                return GameObject.Find("/InverseTeleporter(Clone)")?.transform;
            case SortAPI.TRANSFORM.JackOLantern:
                return GameObject.Find("/PumpkinUnlockableContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.WelcomeMat:
                return GameObject.Find("/WelcomeMatContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Goldfish:
                return GameObject.Find("/FishBowlContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.PlushiePajamaMan:
                return GameObject.Find("/PlushiePJManContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.DiscoBall:
                return GameObject.Find("/DiscoBallContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Microwave:
                return GameObject.Find("/MicrowaveContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.SofaChair:
                return GameObject.Find("/SofaChairContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.Fridge:
                return GameObject.Find("/FridgeContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.ClassicPainting:
                return GameObject.Find("/ClassicPaintingContainer(Clone)")?.transform;
            case SortAPI.TRANSFORM.ElectricChair:
                return GameObject.Find("/ElectricChair(Clone)")?.transform;
            case SortAPI.TRANSFORM.DogHouse:
                return GameObject.Find("/DogHouse(Clone)")?.transform;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EnableDebugMode(StartOfRound sor, GameNetworkManager gnm)
    {
        return sor.IsServer && gnm.disableSteam && sor.connectedPlayersAmount <= 0;
    }
#endif
}
