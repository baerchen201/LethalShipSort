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
        ["", "[ -a | --all ]", "<item> { here | there } [ once | game | always ]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        error = "The ship must be in orbit";
        return StartOfRound.Instance.inShipPhase
            && (
                args.Length < 2
                    ? SortAllItems(args.Contains("-a") || args.Contains("--all"), out error)
                    : SetItemPositionCommand.SetItemPosition(args, out error)
            );
    }

    internal static Coroutine? sorting;

    private static bool SortAllItems(bool all, out string error)
    {
        error = "No items to sort";
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
        if (LethalShipSort.Instance.SortDelay < 10)
        {
            var scrapFailed = 0;
            var toolsFailed = 0;

            if (scrap.Length != 0)
                scrapFailed = SortItems(scrap);
            if (tools.Length != 0)
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
    }

    private static void AutoSortAllItems()
    {
        try
        {
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

            var scrap = items
                .Where(i =>
                    i.itemProperties.isScrap
                    && !(
                        Utils.RemoveClone(i.name) == "ShotgunItem"
                        && cars.Any(car => i.gameObject.transform.parent == car.transform)
                    )
                )
                .ToArray();
            var tools = items
                .Where(i =>
                    !i.itemProperties.isScrap
                    && cars.All(car =>
                        i.gameObject.transform.parent != car.gameObject.gameObject.transform
                    )
                )
                .ToArray();
            if (LethalShipSort.Instance.SortDelay < 10)
            {
                var scrapFailed = 0;
                var toolsFailed = 0;

                if (scrap.Length != 0)
                    scrapFailed = SortItems(scrap);
                if (tools.Length != 0)
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
        GrabbableObject[] scrap,
        GrabbableObject[] tools,
        string errorPrefix = "Error running command"
    )
    {
        var scrapFailed = 0;
        var toolsFailed = 0;

        foreach (var item in scrap)
        {
            try
            {
                if (!Utils.MoveItem(item))
                    scrapFailed++;
            }
            catch (Exception e)
            {
                LethalShipSort.Logger.LogError(e);
                scrapFailed++;
            }

            yield return new WaitForSeconds(delay / 1000f);
        }
        foreach (var item in tools)
        {
            try
            {
                if (!Utils.MoveItem(item))
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

    public static int SortItems(GrabbableObject[] items) =>
        items.Count(item =>
        {
            try
            {
                return !Utils.MoveItem(item);
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
