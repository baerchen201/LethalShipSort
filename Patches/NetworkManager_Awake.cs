using System;
using HarmonyLib;
using Unity.Netcode;

namespace LethalShipSort.Patches;

[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
internal static class NetworkManager_Awake
{
    private static void Postfix(ref NetworkManager __instance)
    {
        __instance.OnClientConnectedCallback += NetworkManager_ClientConnected;
    }

    private static void NetworkManager_ClientConnected(ulong clientId)
    {
        try
        {
            LethalShipSort.Logger.LogDebug(
                $">> {nameof(NetworkManager_ClientConnected)}({nameof(clientId)}: {clientId})"
            );
            var nm = NetworkManager.Singleton;
            if (clientId == 0 || !nm.IsServer)
                return;
            nm.CustomMessagingManager.SendNamedMessage(
                LethalShipSort.NETWORK_MESSAGE_NAME,
                clientId,
                LethalShipSort.Instance.CreateNetworkMessage(),
                NetworkDelivery.ReliableFragmentedSequenced
            );
        }
        catch (Exception e)
        {
            LethalShipSort.Logger.LogError($"Error sharing script to {clientId}: {e}");
        }
    }
}
