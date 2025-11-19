using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChatCommandAPI;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalShipSort;

public class SortItemsCommand : Command
{
    public override string Name => "SortItems";
    public override string[] Commands => ["sort", "ShipSort", Name];
    public override string Description =>
        "Sorts all items on the ship\n-a: sort all items, even items on cruiser";
    public override string[] Syntax =>
        ["", "[ -a | -A ]", "<item> { here | there } [ once | game | always ]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        return StartOfRound.Instance.inShipPhase
            && (
                args.Length < 2
                    ? SortAllItems(
                        args.Contains("-A")
                            || args.Contains("--all", StringComparer.InvariantCultureIgnoreCase)
                                ? ForceLevel.IncludeAll
                            : args.Contains("-a") ? ForceLevel.IncludeCruiser
                            : ForceLevel.None,
                        out error
                    )
                    : SetItemPositionCommand.SetItemPosition(args, out error)
            );
    }

    internal static Coroutine? sorting;

    private enum ForceLevel
    {
        None,
        IncludeCruiser,
        IncludeAll,
    }

    private static bool SortAllItems(ForceLevel forceLevel, out string error)
    {
        error = "No items to sort";
        Utils.objectCount.Clear();
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

        var scrap = FilterFlags(
            items
                .Where(i => i.itemProperties.isScrap)
                .ToDictionary(i => i, item => LethalShipSort.Instance.GetPosition(item))
        );

        var tools = FilterFlags(
            items
                .Where(i => !i.itemProperties.isScrap)
                .ToDictionary(i => i, item => LethalShipSort.Instance.GetPosition(item))
        );

        if (LethalShipSort.Instance.SortDelay < 10)
        {
            var scrapFailed = 0;
            var toolsFailed = 0;

            if (scrap.Count != 0)
                scrapFailed = SortItems(scrap);
            if (tools.Count != 0)
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

        Dictionary<GrabbableObject, ItemPosition> FilterFlags(
            Dictionary<GrabbableObject, ItemPosition> dict
        )
        {
            return dict.Where(kvp =>
                    (forceLevel == ForceLevel.IncludeAll || !kvp.Value.flags.Ignore)
                    && (
                        forceLevel >= ForceLevel.IncludeCruiser
                        || !(
                            kvp.Value.flags.KeepOnCruiser
                            && cars.Any(car => kvp.Key.transform.parent == car.transform)
                        )
                    )
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    private static void AutoSortAllItems()
    {
        try
        {
            Utils.objectCount.Clear();
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

            var scrap = FilterFlags(
                items
                    .Where(i => i.itemProperties.isScrap)
                    .ToDictionary(i => i, item => LethalShipSort.Instance.GetPosition(item))
            );
            var tools = FilterFlags(
                items
                    .Where(i => !i.itemProperties.isScrap)
                    .ToDictionary(i => i, item => LethalShipSort.Instance.GetPosition(item))
            );
            if (LethalShipSort.Instance.SortDelay < 10)
            {
                var scrapFailed = 0;
                var toolsFailed = 0;

                if (scrap.Count != 0)
                    scrapFailed = SortItems(scrap);
                if (tools.Count != 0)
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
            return;

            Dictionary<GrabbableObject, ItemPosition> FilterFlags(
                Dictionary<GrabbableObject, ItemPosition> dict
            )
            {
                return dict.Where(kvp =>
                        kvp.Value.flags is { Ignore: false, NoAutoSort: false }
                        && !(
                            kvp.Value.flags.KeepOnCruiser
                            && cars.Any(car => kvp.Key.transform.parent == car.transform)
                        )
                    )
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
        Dictionary<GrabbableObject, ItemPosition> scrap,
        Dictionary<GrabbableObject, ItemPosition> tools,
        string errorPrefix = "Error running command"
    )
    {
        var scrapFailed = 0;
        var toolsFailed = 0;

        foreach (var kvp in scrap)
        {
            try
            {
                if (!Utils.MoveItem(kvp.Key, kvp.Value))
                    scrapFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                scrapFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }
        foreach (var kvp in tools)
        {
            try
            {
                if (!Utils.MoveItem(kvp.Key, kvp.Value))
                    toolsFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                toolsFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }

        var error =
            $"{(scrapFailed > 0 ? $"{scrapFailed} scrap items {(toolsFailed > 0 ? "and " : "")}" : "")}{(toolsFailed > 0 ? $"{toolsFailed} tool items" : "")} couldn't be sorted";

        if (scrapFailed != 0 || toolsFailed != 0)
            ChatCommandAPI.ChatCommandAPI.PrintError(
                $"{errorPrefix}: <noparse>" + error + "</noparse>"
            );
        else
            ChatCommandAPI.ChatCommandAPI.Print("Finished sorting items");
    }

    public static int SortItems(Dictionary<GrabbableObject, ItemPosition> items) =>
        items.Count(kvp =>
        {
            try
            {
                return !Utils.MoveItem(kvp.Key, kvp.Value);
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
