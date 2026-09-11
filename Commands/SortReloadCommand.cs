using ChatCommandAPI;
using ChatCommandAPI.Utils;
using Unity.Netcode;

namespace LethalShipSort.Commands;

public class SortReloadCommand : Command
{
    public override string Name => "ReloadShipSort";

    public override string Description => "Reloads your sorting script from disk";

    public override string Command => "sort-reload";

    public override void Invoke(string args)
    {
        var mod = LethalShipSort.Instance;
        try
        {
            mod.ReloadScript();
            Chat.Print("Script reloaded successfully.");
        }
        finally
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsServer)
                nm.CustomMessagingManager.SendNamedMessageToAll(
                    LethalShipSort.NETWORK_MESSAGE_NAME,
                    mod.CreateNetworkMessage(),
                    NetworkDelivery.ReliableFragmentedSequenced
                );
        }
    }
}
