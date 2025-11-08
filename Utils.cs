using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Random = System.Random;

namespace LethalShipSort;

public static class Utils
{
    private const string CLONE = "(Clone)";

    public static string RemoveClone(string name) =>
        name.EndsWith(CLONE) ? name[..^CLONE.Length] : name;

    public static bool MoveItem(GrabbableObject item, ItemPosition position) =>
        position.position == null
            ? throw new ArgumentNullException(
                $"{nameof(ItemPosition)}.{nameof(ItemPosition.position)} can not be null"
            )
        : position.flags.Parent && position.parentTo != null
            ? MoveItem(
                item,
                position.position.Value,
                position.parentTo,
                position.floorYRot ?? -1,
                position.flags
            )
        : MoveItemRelativeTo(
            item,
            position.position.Value,
            position.parentTo,
            position.floorYRot ?? -1,
            position.flags
        );

    public static bool MoveItemRelativeTo(
        GrabbableObject item,
        Vector3 position,
        GameObject? relativeTo,
        int floorYRot,
        ItemPosition.Flags flags
    )
    {
        LethalShipSort.Logger.LogDebug(
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
                    ship.transform.InverseTransformPoint(hitInfo.point + item.itemProperties.verticalOffset * Vector3.up),
                    LethalShipSort.Instance.RandomOffsetValue
                );
            else
            {
                LethalShipSort.Logger.LogWarning("   Raycast unsuccessful");
                return false;
            }
        else
            position = Randomize(position + item.itemProperties.verticalOffset * Vector3.up, LethalShipSort.Instance.RandomOffsetValue);

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
        GameObject parentTo,
        int floorYRot,
        ItemPosition.Flags flags
    )
    {
        LethalShipSort.Logger.LogDebug(
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
                            - new Vector3(0f, 0.05f, 0f),
                        LethalShipSort.Instance.RandomOffsetValue
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
                LethalShipSort.Instance.RandomOffsetValue
            );

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

    public static Vector3 Randomize(Vector3 position, float maxDistance = 0.05f)
    {
        if (maxDistance < 0)
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
        var parent = gameObject.transform.parent;
        var path = gameObject.name;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.transform.parent;
        }

        return path;
    }

    internal const int LAYER_MASK = (int)(Layers.Room | Layers.Colliders | Layers.Railing);

    [Flags]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    private enum Layers
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
