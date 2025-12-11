using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace LethalShipSort;

public static class Utils
{
    private const string CLONE = "(Clone)";

    public static string RemoveClone(string name) =>
        name.EndsWith(CLONE) ? name[..^CLONE.Length] : name;

    public static bool MoveItem(GrabbableObject item, ItemPosition position)
    {
        if (position.position == null)
            throw new ArgumentNullException(
                $"{nameof(ItemPosition)}.{nameof(ItemPosition.position)} can not be null"
            );
        var positionOffset =
            position.positionOffset != null
                ? position.positionOffset.Value * GetItemCount(item)
                : Vector3.zero;
        var floorYRot =
            (position.floorYRot ?? -1) + (position.rotationOffset ?? 0) * GetItemCount(item) % 360;
        return (
                position.flags.Parent && position.parentTo != null
                    ? MoveItem(
                        item,
                        position.position.Value,
                        positionOffset,
                        position.parentTo,
                        floorYRot,
                        position.randomOffset,
                        position.flags
                    )
                    : MoveItemRelativeTo(
                        item,
                        position.position.Value,
                        positionOffset,
                        position.parentTo,
                        floorYRot,
                        position.randomOffset,
                        position.flags
                    )
            ) && IncreaseItemCount(item);
    }

    internal static readonly Dictionary<string, int> objectCount = [];

    public static IReadOnlyDictionary<string, int> ObjectCount => objectCount;

    private static bool IncreaseItemCount(GrabbableObject item)
    {
        var key = ItemKey(item);
        objectCount[key] = objectCount.GetValueOrDefault(key) + 1;
        return true;
    }

    private static int GetItemCount(GrabbableObject item) =>
        objectCount.GetValueOrDefault(ItemKey(item));

    /// <summary>
    /// You can patch this method to add sub-categories of items (like different amounts of bullets in a shotgun)
    /// </summary>
    /// <param name="item">The item to get a key for</param>
    /// <returns>A unique string representation of the item type</returns>
    public static string ItemKey(GrabbableObject item) => RemoveClone(item.name);

    public static bool MoveItemRelativeTo(
        GrabbableObject item,
        Vector3 position,
        Vector3 positionOffset,
        GameObject? relativeTo,
        int floorYRot,
        float? randomOffset,
        ItemPosition.Flags flags
    )
    {
        LethalShipSort.Logger.LogInfo(
            $">> Moving item {RemoveClone(item.name)} to position {position} relative to {(relativeTo == null ? "ship" : RemoveClone(relativeTo.name))}"
        );

        var ship = GameObject.Find("Environment/HangarShip");
        if (ship == null)
        {
            LethalShipSort.Logger.LogWarning("   Couldn't find ship");
            return false;
        }

        if (relativeTo == null)
            relativeTo = ship;
        if (!flags.Exact)
            if (
                Physics.Raycast(
                    relativeTo.transform.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                )
            )
                position = Randomize(
                    ship.transform.InverseTransformPoint(
                        hitInfo.point
                            + item.itemProperties.verticalOffset * Vector3.up
                            + positionOffset
                    ),
                    randomOffset
                );
            else
            {
                LethalShipSort.Logger.LogWarning("   Raycast unsuccessful");
                return false;
            }
        else
            position = Randomize(
                position + item.itemProperties.verticalOffset * Vector3.up + positionOffset,
                randomOffset
            );

        LethalShipSort.Logger.LogDebug($"   true position: {position}");
        GameNetworkManager.Instance.localPlayerController.SetObjectAsNoLongerHeld(
            true,
            true,
            position,
            item,
            floorYRot
        );
        GameNetworkManager.Instance.localPlayerController.ThrowObjectServerRpc(
            item.NetworkObject,
            true,
            true,
            position,
            floorYRot
        );
        return true;
    }

    public static bool MoveItem(
        GrabbableObject item,
        Vector3 position,
        Vector3 positionOffset,
        GameObject parentTo,
        int floorYRot,
        float? randomOffset,
        ItemPosition.Flags flags
    )
    {
        LethalShipSort.Logger.LogInfo(
            $">> Moving item {RemoveClone(item.name)} to position {position} in {RemoveClone(parentTo.name)}"
        );

        if (!flags.Exact)
            if (
                Physics.Raycast(
                    parentTo.transform.TransformPoint(position),
                    Vector3.down,
                    out var hitInfo,
                    80f,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                )
            )
                position = parentTo.transform.InverseTransformPoint(
                    Randomize(
                        hitInfo.point
                            + item.itemProperties.verticalOffset * Vector3.up
                            - new Vector3(0f, 0.05f, 0f)
                            + positionOffset,
                        randomOffset
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
                    - new Vector3(0f, 0.05f, 0f)
                    + positionOffset,
                randomOffset
            );

        LethalShipSort.Logger.LogDebug($"   true position: {position}");
        GameNetworkManager.Instance.localPlayerController.SetObjectAsNoLongerHeld(
            true,
            true,
            position,
            item,
            floorYRot
        );
        GameNetworkManager.Instance.localPlayerController.ThrowObjectServerRpc(
            item.NetworkObject,
            true,
            true,
            position,
            floorYRot
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

    public static Vector3 Randomize(Vector3 position, float? maxDistance = null)
    {
        if (maxDistance < 0)
            throw new ArgumentException("Invalid randomOffset (must be positive)");
        else if (maxDistance == null || Mathf.Approximately(maxDistance.Value, 0f))
            return position;
        Random rng = new();
        return new Vector3(
            position.x + (float)rng.NextDouble() * maxDistance.Value * 2 - maxDistance.Value,
            position.y,
            position.z + (float)rng.NextDouble() * maxDistance.Value * 2 - maxDistance.Value
        );
    }

    public static string GameObjectPath(GameObject gameObject)
    {
        var parent = gameObject.transform.parent;
        var path = gameObject.name;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.transform.parent;
        }

        return path;
    }

    internal const int LAYER_MASK = 268437761; // Copied this straight from GrabbableObject.GetItemFloorPosition
}
