using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using ChatCommandAPI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalShipSort;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI")]
public class LethalShipSort : BaseUnityPlugin
{
    public static LethalShipSort Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        _ = new SortItemsCommand();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }
}

public class SortItemsCommand : Command
{
    public override string Name => "SortItems";
    public override string[] Commands => [Name, "Sort", "Organize"];
    public override string Description =>
        "Sorts all items on the ship\n-a: sort all items, even items on cruiser";
    public override string[] Syntax => ["", "[ -a | --all ]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        return StartOfRound.Instance.inShipPhase
            && SortAllItems(args.Contains("-a") || args.Contains("--all"), out error);
    }

    private static bool SortAllItems(bool all, out string error)
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
        int scrapFailed = 0;
        int toolsFailed = 0;
        if (scrap.Length != 0)
            scrapFailed = SortItems(scrap);

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
        if (tools.Length != 0)
            toolsFailed = SortItems(tools);

        error =
            $"{(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted";

        return scrapFailed == 0 && toolsFailed == 0;
    }

    public static int SortItems(GrabbableObject[] items) =>
        items.Count(item => !Utils.MoveItem(item));
}

public static class Utils
{
    private const string CLONE = "(Clone)";

    public static string RemoveClone(string name) =>
        name.EndsWith(CLONE) ? name[..^CLONE.Length] : name;

    public static bool MoveItem(GrabbableObject item)
    {
        return item.itemProperties.isScrap
            ? MoveItem(item, Positions.GetPosition(item))
            : MoveItemToCloset(item, Positions.GetPosition(item));
    }

    public static bool MoveItem(GrabbableObject item, Vector3 position)
    {
        LethalShipSort.Logger.LogDebug(
            $">> Moving item {RemoveClone(item.name)} to position {position}"
        );
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
                position = Positions.Randomize(
                    ship.InverseTransformPoint(
                        hitInfo.point + item.itemProperties.verticalOffset * Vector3.up
                    )
                );
            else
            {
                LethalShipSort.Logger.LogWarning("   Raycast unsuccessful");
                return false;
            }
        else
        {
            LethalShipSort.Logger.LogWarning("   Couldn't find ship");
            return false;
        }

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

    public static bool MoveItemToCloset(GrabbableObject item, Vector3 position)
    {
        LethalShipSort.Logger.LogDebug(
            $">> Moving item {RemoveClone(item.name)} to position {position} in closet"
        );
        Transform closet = GameObject.Find("Environment/HangarShip/StorageCloset").transform;
        if (closet != null)
            if (
                Physics.Raycast(
                    closet.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    1073744640,
                    QueryTriggerInteraction.Ignore
                )
            )
                position = closet.InverseTransformPoint(
                    Positions.Randomize(
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
        {
            LethalShipSort.Logger.LogWarning("   Couldn't find closet");
            return false;
        }

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
            closet,
            position,
            false,
            item
        );
        GameNetworkManager.Instance.localPlayerController.PlaceObjectServerRpc(
            item.NetworkObject,
            closet.gameObject,
            position,
            false
        );
        return true;
    }
}
