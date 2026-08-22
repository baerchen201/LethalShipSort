using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using LethalModUtils;
using Unity.Netcode;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Initialize))]
internal static class NetworkManager_Initialize
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        return new CodeMatcher(instructions)
            .MatchForward(
                new CodeMatch(
                    OpCodes.Call,
                    AccessTools.PropertySetter(
                        typeof(NetworkManager),
                        nameof(NetworkManager.CustomMessagingManager)
                    )
                )
            )
            .Insert(
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(
                        typeof(NetworkManager_Initialize),
                        nameof(RegisterNetworkMessageHandler)
                    )
                )
            )
            .InstructionEnumeration();
    }

    private static void RegisterNetworkMessageHandler(CustomMessagingManager customMessagingManager)
    {
        var mod = LethalShipSort.Instance;
        mod.ClearSharedScript();
        customMessagingManager.RegisterNamedMessageHandler(
            LethalShipSort.NETWORK_MESSAGE_NAME,
            LethalShipSort.NetworkMessageHandler
        );
        LethalShipSort.Logger.LogInfo("Registered network message handler");
        mod.ReloadScript();
    }
}
