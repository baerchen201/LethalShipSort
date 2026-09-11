using HarmonyLib;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
internal static class StartOfRound_StartGame
{
    private static void Prefix(ref StartOfRound __instance)
    {
        LethalShipSort.TryAutoSort(__instance);
    }
}
