using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ChatCommandAPI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalShipSort;

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
        Utils.objectCount.Clear();
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
            else if (
                !LethalShipSort.Instance.modItems.TryGetValue(name.ToLower(), out internalName)
            )
                if (LethalShipSort.Instance.modItems.ContainsValue(name.ToLower()))
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
                if (
                    LethalShipSort.Instance.modItemPositions.All(kvp =>
                        !string.Equals(
                            kvp.Key,
                            internalName,
                            StringComparison.CurrentCultureIgnoreCase
                        )
                    )
                )
                    return false;
                else
                    config = LethalShipSort
                        .Instance.modItemPositions.First(kvp =>
                            string.Equals(
                                kvp.Key,
                                internalName,
                                StringComparison.CurrentCultureIgnoreCase
                            )
                        )
                        .Value;
            else
                config = LethalShipSort
                    .Instance.itemPositions.First(kvp =>
                        string.Equals(
                            kvp.Key,
                            internalName,
                            StringComparison.CurrentCultureIgnoreCase
                        )
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
                    var itemsFailed = items.Count(item =>
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
                goto sort;
            case when.always:
                config!.Value =
                    relativeTo == null
                        ? $"none:{position.x},{position.y},{position.z}"
                        : $"{Utils.GameObjectPath(relativeTo)}:{position.x},{position.y},{position.z}";
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"Items of type {internalName} will be put on position {(relativeTo == null ? position : relativeTo.transform.InverseTransformPoint(position))}"
                );
                sort:

                items = Object.FindObjectsOfType<GrabbableObject>();
                if (items == null)
                    break;
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
                    break;

                if (LethalShipSort.Instance.SortDelay < 10)
                {
                    var itemsFailed = items.Count(item =>
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
                }
                break;
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
        var itemsFailed = 0;

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

        var error = $"{itemsFailed} items couldn't be sorted";

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
                        Utils.LAYER_MASK,
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
                        Utils.LAYER_MASK,
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
