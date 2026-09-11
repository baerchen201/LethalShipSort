using System.Collections;
using HarmonyLib;

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
        yield return null;
        LethalShipSort.TryAutoSort(__instance);
    }
}
