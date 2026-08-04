#if DEBUG
using ChatCommandAPI.Utils;
using HarmonyLib;
using LethalShipSort.Commands;
using UnityEngine;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItemOnClient))]
internal static class GrabbableObject_DiscardItemOnClient
{
    internal static bool enable = true;

    private static void Postfix(ref GrabbableObject __instance)
    {
        if (enable && StartOfRound.Instance.connectedPlayersAmount <= 0)
        {
            var warn = false;
            var pos =
                (
                    __instance.transform.parent?.TransformPoint(__instance.targetFloorPosition)
                    ?? __instance.targetFloorPosition
                )
                + (
                    __instance.itemProperties != null
                        ? __instance.itemProperties.verticalOffset * Vector3.down
                        : Vector3.zero
                );
            var rel = SortHelperCommand.SortHelper;

            recalc:
            var transform = LethalShipSort.GetTransform(rel, out _);
            if (transform != null)
            {
                var message =
                    $"======\nItem {__instance} dropped at\n{transform.InverseTransformPoint(pos)}\n(relative to {rel})";
                if (warn)
                    Chat.PrintWarning(message);
                else
                    Chat.Print(message);
                return;
            }

            warn = true;

            switch (rel)
            {
                case SortAPI.TRANSFORM.Ship:
                    rel = SortAPI.TRANSFORM.World;
                    goto recalc;
                case SortAPI.TRANSFORM.World:
                    Chat.PrintError("[SortHelper] fuck you");
                    return;
                default:
                    rel = SortAPI.TRANSFORM.Ship;
                    goto recalc;
            }
        }
    }
}
#endif
