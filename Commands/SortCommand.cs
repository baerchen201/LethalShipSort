using System;
using System.Linq;
using ChatCommandAPI;
using ChatCommandAPI.Utils;
using Lua;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
#if DEBUG
using LethalShipSort.Patches;
#endif

namespace LethalShipSort.Commands;

public class SortCommand : Command
{
    public override string Name => "ShipSort";

    public override string Description => "Manually sorts your ship";

    public override string Command => "sort";

    public override void Invoke(string args)
    {
        var sor = StartOfRound.Instance;
#if DEBUG
        if (!sor.inShipPhase && !LethalShipSort.EnableDebugMode(sor, GameNetworkManager.Instance))
#else
        if (!sor.inShipPhase)
#endif
            throw new ShipIsLandedException();

        var mod = LethalShipSort.Instance;

        if (!string.IsNullOrWhiteSpace(args))
            if (args.Trim().ToLowerInvariant() == "reload")
            {
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

                return;
            }

        var items = Object.FindObjectsByType<GrabbableObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID
        );
        var l = items.Length;
        items = LethalShipSort.FilterItems(items, sor.localPlayerController).ToArray();
        if (items.Length <= 0)
        {
            Chat.Print("There are no items to sort");
            return;
        }

#if DEBUG
        GrabbableObject_DiscardItemOnClient.enable = false;
#endif
        try
        {
            if (
                !mod.Sort(
                    items,
                    sor.localPlayerController,
                    RoundManager.Instance.currentLevel,
                    TimeOfDay.Instance.daysUntilDeadline,
                    Object.FindAnyObjectByType<VehicleController>(FindObjectsInactive.Exclude)
                        != null,
                    Object.FindFirstObjectByType<ShipLights>().areLightsOn,
                    sor.unlockablesList,
                    Args.Parse(args),
                    (uint)(l - items.Length)
                )
            )
                throw new CommandException($"Script '{mod.ScriptPath}' could not be found");
        }
        catch (ArgumentException e)
        {
            throw new CommandException($"Script result invalid: {e.Message}");
        }
        catch (TimeoutException)
        {
            throw new CommandException("Script execution timed out");
        }
        catch (LuaCompileException e)
        {
            LethalShipSort.Logger.LogError(e);
            throw new CommandException(
                $"Script compilation error: {e.Message.Trim()}\nCheck the logs for more details"
            );
        }
        catch (LuaRuntimeException e)
        {
            LethalShipSort.Logger.LogDebug(e);
            LethalShipSort.Logger.LogError(e.LuaTraceback);
            throw new CommandException(
                $"Script error: {e.Message.Trim()}\nCheck the logs for more details"
            );
        }
#if DEBUG
        finally
        {
            GrabbableObject_DiscardItemOnClient.enable = true;
        }
#endif
    }
}
