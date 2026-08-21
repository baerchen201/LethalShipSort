using System;
using System.IO;
using ChatCommandAPI;
using ChatCommandAPI.Utils;
using Lua;
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
        var items = Object.FindObjectsByType<GrabbableObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID
        );
        var l = items.Length;
        items = LethalShipSort.FilterItems(items, sor.localPlayerController);
        if (items.Length <= 0)
            throw new CommandException("There are no items to sort");

#if DEBUG
        GrabbableObject_DiscardItemOnClient.enable = false;
#endif
        try
        {
            Chat.Print(
                LethalShipSort.Instance.Sort(
                    items,
                    sor.localPlayerController,
                    RoundManager.Instance.currentLevel,
                    TimeOfDay.Instance.daysUntilDeadline,
                    Object.FindAnyObjectByType<VehicleController>(FindObjectsInactive.Exclude)
                        != null,
                    Object.FindFirstObjectByType<ShipLights>().areLightsOn,
                    sor.unlockablesList,
                    (uint)(l - items.Length)
                )
            );
        }
        catch (FileNotFoundException e)
        {
            throw new CommandException($"Script '{e.FileName}' could not be found");
        }
        catch (ArgumentException e)
        {
            throw new CommandException($"Script result invalid: {e.Message}");
        }
        catch (TimeoutException)
        {
            throw new CommandException("Script execution timed out");
        }
        catch (LuaRuntimeException e)
        {
            LethalShipSort.Logger.LogDebug(e);
            LethalShipSort.Logger.LogError(e.LuaTraceback);
            throw new CommandException(
                $"Script error: {e.Message}\nCheck the logs for more details"
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
