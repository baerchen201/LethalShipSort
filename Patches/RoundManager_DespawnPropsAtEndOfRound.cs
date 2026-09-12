using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
internal static class RoundManager_DespawnPropsAtEndOfRound
{
    private static void Postfix(ref RoundManager __instance)
    {
        __instance.playersManager.StartCoroutine(DelayedAutoSort(__instance.playersManager));
    }

    private static IEnumerator DelayedAutoSort(StartOfRound __instance)
    {
        yield return new WaitForSecondsRealtime(2);
        LethalShipSort.TryAutoSort(__instance);
    }
}
