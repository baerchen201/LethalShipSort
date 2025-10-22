using System;
using System.Collections.Generic;
using System.Linq;
using ChatCommandAPI;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace LethalShipSort;

public class PrintItemNames : Command
{
    public override string[] Commands => ["itemnames", Name];
    public override string Description => "Lists all currently loaded item names";
    public override string[] Syntax => ["", "[ -a | --all ]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string? error)
    {
        error = "The ship must be in orbit";
        if (!StartOfRound.Instance.inShipPhase)
            return false;

        var all = args.Contains("-a") || args.Contains("--all");

        error = "No items on the ship";
        Dictionary<string, string?> list = [];
        foreach (var item in Object.FindObjectsOfType<GrabbableObject>())
        {
            var name = Utils.RemoveClone(item.name);
            if (all || !LethalShipSort.Instance.vanillaItems.ContainsValue(name.ToLower()))
                list.TryAdd(name, item.itemProperties?.itemName);
        }

        if (list.Count == 0)
            return false;

        var l = string.Join(
            '\n',
            list.Select(kvp => $"{kvp.Key}{(kvp.Value != null ? $": {kvp.Value}" : "")}")
                .OrderBy(i => i, StringComparer.CurrentCultureIgnoreCase)
        );
        var m = $"The following{(all ? "" : " unknown")} items are currently on the ship:\n";
        ChatCommandAPI.ChatCommandAPI.Print($"{m}<indent=10px>{l}</indent>");
        LethalShipSort.Logger.LogInfo(m + l);
        return true;
    }
}
