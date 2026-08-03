#if false
using ChatCommandAPI.Utils;
using HarmonyLib;
using LethalShipSort.Commands;
using UnityEngine;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.GetItemFloorPosition))]
internal static class GrabbableObject_GetItemFloorPosition
{
    private static void Postfix(ref Vector3 startPosition, ref GrabbableObject __instance)
    {
        if (startPosition == Vector3.zero)
            startPosition = __instance.transform.position + Vector3.up * 0.15f;
        if (
            Physics.Raycast(
                startPosition,
                -Vector3.up,
                out var hitInfo,
                80f,
                LethalShipSort.LAYER_MASK,
                QueryTriggerInteraction.Ignore
            )
        )
            startPosition = hitInfo.point + Vector3.up * 0.04f;

        Chat.Print(
            $"======\nItem {__instance} dropped at\n{LethalShipSort.GetTransform(SortHelperCommand.SortHelper, out _).InverseTransformPoint(startPosition)}\n(relative to {SortHelperCommand.SortHelper})"
        );
    }
}
#endif