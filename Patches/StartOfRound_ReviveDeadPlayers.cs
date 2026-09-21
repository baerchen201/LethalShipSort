using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers))]
internal static class StartOfRound_ReviveDeadPlayers
{
    private static void Postfix(ref StartOfRound __instance)
    {
        __instance.StartCoroutine(DelayedAutoSort(__instance));
    }

    private static IEnumerator DelayedAutoSort(StartOfRound __instance)
    {
        yield return new WaitForSecondsRealtime(2);
        LethalShipSort.TryAutoSort(__instance);
    }
}
