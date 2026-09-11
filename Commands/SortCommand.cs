using ChatCommandAPI;
using ChatCommandAPI.Utils;
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
#if DEBUG
        GrabbableObject_DiscardItemOnClient.enable = false;
        try
#endif
        {
            if (!LethalShipSort.Instance.Sort(StartOfRound.Instance, Args.Parse(args)))
                Chat.Print("There are no items to sort");
        }
#if DEBUG
        finally
        {
            GrabbableObject_DiscardItemOnClient.enable = true;
        }
#endif
    }
}
