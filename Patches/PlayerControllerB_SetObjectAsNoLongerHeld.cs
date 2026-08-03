#if DEBUG
using ChatCommandAPI.Utils;
using HarmonyLib;
using LethalShipSort.Commands;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItemOnClient))]
internal static class GrabbableObject_DiscardItemOnClient
{
    internal static bool enable;

    private static void Postfix(ref GrabbableObject __instance)
    {
        if (enable)
            Chat.Print(
                $"======\nItem {__instance} dropped at\n{LethalShipSort.GetTransform(SortHelperCommand.SortHelper, out _).InverseTransformPoint(__instance.transform.parent?.TransformPoint(__instance.targetFloorPosition) ?? __instance.targetFloorPosition)}\n(relative to {SortHelperCommand.SortHelper})"
            );
    }
}
#endif