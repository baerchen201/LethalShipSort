using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChatCommandAPI;
using Unity.Netcode;
using UnityEngine;
using Logger = UnityEngine.Logger;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace LethalShipSort;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI", BepInDependency.DependencyFlags.HardDependency)]
public class LethalShipSort : BaseUnityPlugin
{
    public static LethalShipSort Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        _ = new PositionExtract();

        if (Positions.SCRAP_POSITIONS.Length == 0)
            Logger.LogError("No scrap positions");
        if (Positions.TOOL_POSITIONS.Length == 0)
            Logger.LogError("No tool positions");
        if (Positions.SCRAP_POSITIONS.Length == 0 || Positions.TOOL_POSITIONS.Length == 0)
        {
            Logger.LogError(
                $"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has failed to load! (PositionExtract loaded)"
            );
            return;
        }

        _ = new SortItemsCommand();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }
}

public class SortItemsCommand : Command
{
    public enum SORT_MODE
    {
        SORTED,
        RANDOM,
        NAMED,
        Length,
    }

    public override string Name => "SortItems";
    public override string[] Commands => [Name, "Sort", "Organize"];
    public override string Description => "Sorts all items on the ship";

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        displayedWarning = false;
        return StartOfRound.Instance.inShipPhase && SortAllItems(out error, SORT_MODE.NAMED);
    }

    public static readonly Vector3[] ScrapPositions = Positions
        .SCRAP_POSITIONS.Select(i => new Vector3(i.Item1, Positions.Y, i.Item2))
        .ToArray();
    public static readonly Vector3[] ToolPositions = Positions
        .TOOL_POSITIONS.Select(i => new Vector3(i.Item1, Positions.Y, i.Item2))
        .ToArray();

    public static readonly Vector3 FallbackPosition = new(
        Positions.FALLBACK_POSITION.Item1,
        Positions.Y,
        Positions.FALLBACK_POSITION.Item2
    );
    public static readonly Dictionary<string, Vector3> NamedScrapPositions = Positions
        .NAMED_POSITIONS.Select(kvp =>
            (kvp.Key, new Vector3(kvp.Value.Item1, Positions.Y, kvp.Value.Item2))
        )
        .ToDictionary(kvp => kvp.Item1, kvp => kvp.Item2);

    private const string WARNING =
        "You have too many different items on the ship.\nSome items may be stacked on others";
    private const string NAMED_WARNING =
        "You have some items on the ship, that don't have a specified position.\nSome items may be stacked on others";
    private bool displayedWarning;

    private const string CLONE = "(Clone)";

    public bool SortAllItems(out string error, SORT_MODE sortMode = SORT_MODE.SORTED)
    {
        error = "No items to sort";
        var items = Object.FindObjectsOfType<GrabbableObject>();
        if (items == null)
            return false;
        items = items
            .Where(i => i is { playerHeldBy: null } and not RagdollGrabbableObject)
            .ToArray();
        if (items.Length == 0)
            return false;

        var scrap = items.Where(i => i.itemProperties.isScrap).ToArray();
        int scrapFailed = 0;
        int toolsFailed = 0;
        if (scrap.Length != 0)
            scrapFailed = sortMode switch
            {
                SORT_MODE.NAMED => SortItems(scrap, NamedScrapPositions),
                _ => SortItems(scrap, ScrapPositions, sortMode == SORT_MODE.SORTED),
            };
        goto _;
        var tools = items.Where(i => !i.itemProperties.isScrap).ToArray();
        // ReSharper disable once InvertIf
        if (tools.Length != 0)
            toolsFailed = SortItems(tools, ToolPositions);

        _:
        error =
            $"{(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted";

        return scrapFailed == 0 && toolsFailed == 0;
    }

    private int SortItems(GrabbableObject[] items, Vector3[] positions, bool sortBeforeSort = true)
    {
        if (sortBeforeSort)
        {
            Array.Sort(items, (l, r) => new CaseInsensitiveComparer().Compare(l.name, r.name));
            Array.Sort(
                items,
                (l, r) =>
                    l.itemProperties.twoHanded switch
                    {
                        true => r.itemProperties.twoHanded ? 0 : -1,
                        false => r.itemProperties.twoHanded ? 1 : 0,
                    }
            );
        }
        else
        {
            var r = new Random();
            items = items
                .Select(x => (x, r.Next()))
                .OrderBy(i => i.Item2)
                .Select(i => i.Item1)
                .ToArray();
        }

        string p = null!;
        int i = 0;
        int f = 0;
        foreach (var item in items)
        {
            if (item.name != p)
            {
                p = item.name;
                i++;
            }

            if (!displayedWarning && i > positions.Length)
            {
                displayedWarning = true;
                ChatCommandAPI.ChatCommandAPI.PrintWarning(WARNING);
            }

            if (!MoveItem(item, positions[i % positions.Length]))
                f++;
        }

        return f;
    }

    private int SortItems(GrabbableObject[] items, Dictionary<string, Vector3> positions)
    {
        Array.Sort(items, (l, r) => new CaseInsensitiveComparer().Compare(l.name, r.name));
        Array.Sort(
            items,
            (l, r) =>
                l.itemProperties.twoHanded switch
                {
                    true => r.itemProperties.twoHanded ? 0 : -1,
                    false => r.itemProperties.twoHanded ? 1 : 0,
                }
        );
        int f = 0;
        foreach (var item in items)
        {
            if (
                !positions.TryGetValue(
                    item.name.EndsWith(CLONE) ? item.name[..^CLONE.Length] : item.name,
                    out var position
                )
            )
            {
                position = FallbackPosition;
                LethalShipSort.Logger.LogWarning(
                    $"Position for item {(item.name.EndsWith(CLONE) ? item.name[..^CLONE.Length] : item.name)} not found"
                );
                if (!displayedWarning)
                {
                    displayedWarning = true;
                    ChatCommandAPI.ChatCommandAPI.PrintWarning(NAMED_WARNING);
                }
            }
            else
                LethalShipSort.Logger.LogDebug(
                    $"Position for item {(item.name.EndsWith(CLONE) ? item.name[..^CLONE.Length] : item.name)}: {position}"
                );

            if (!MoveItem(item, position))
                f++;
        }

        return f;
    }

    private static bool MoveItem(GrabbableObject item, Vector3 position)
    {
        LethalShipSort.Logger.LogDebug($"Moving item {item.name} to position {position}");
        Transform ship = GameObject.Find("Environment/HangarShip").transform;
        if (ship != null)
            if (
                Physics.Raycast(
                    ship.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    268437760,
                    QueryTriggerInteraction.Ignore
                )
            )
                position = ship.InverseTransformPoint(
                    hitInfo.point + item.itemProperties.verticalOffset * Vector3.up
                );
            else
                LethalShipSort.Logger.LogWarning("Raycast unsuccessful");
        else
            LethalShipSort.Logger.LogWarning("Couldn't find ship");

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
}

public class PositionExtract : Command
{
    public override string[] Commands => ["sortPositionExtract"];
    public override bool Hidden => true;

    private Transform ship = null!;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "Couldn't find ship";
        ship = GameObject.Find("Environment/HangarShip").transform;
        if (ship == null)
            return false;
        StringBuilder sb = new StringBuilder("\n[\n"),
            sb2 = new StringBuilder("\nnew Dictionary<string, (float, float)>()\n{\n");
        int i = 0,
            j = 0;
        foreach (var kvp in ItemList.GoodItems)
        {
            sb.Append(Extract(kvp.Value));
            sb2.Append(Extract(kvp.Key, kvp.Value));
            i++;
        }
        sb.Append("\n");
        sb2.Append("\n");
        foreach (var kvp in ItemList.BadItems)
        {
            sb.Append(Extract(kvp.Value));
            sb2.Append(Extract(kvp.Key, kvp.Value));
            j++;
        }
        sb.Append("];");
        sb2.Append("};");
        LethalShipSort.Logger.LogInfo(sb.ToString());
        LethalShipSort.Logger.LogInfo(sb2.ToString());
        ChatCommandAPI.ChatCommandAPI.Print($"Printed {i}+{j} positions to log");
        return true;
    }

    private string Extract(Vector3 pos)
    {
        Vector3 localPos = ship.InverseTransformPoint(pos);
        return $"  ({Math.Round(localPos.x, 2)}f, {Math.Round(localPos.z, 2)}f),\n";
    }

    private string Extract(string key, Vector3 pos)
    {
        Vector3 localPos = ship.InverseTransformPoint(pos);
        return $"  [\"{key}\"] = ({Math.Round(localPos.x, 2)}f, {Math.Round(localPos.z, 2)}f),\n";
    }
}
