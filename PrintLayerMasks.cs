using System;
using System.Collections.Generic;
using ChatCommandAPI;
using UnityEngine;

namespace LethalShipSort;

[Obsolete]
internal class PrintLayerMasks : Command
{
    public override bool Hidden => true;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        LethalShipSort.Logger.LogInfo("LayerMask MoveItemRelativeTo:");
        for (var i = 0; i < 32; i++)
        {
            LethalShipSort.Logger.LogInfo(
                $" {((268437760 & (1 << i)) != 0 ? "o" : "-")} {LayerMask.LayerToName(i)}"
            );
        }
        LethalShipSort.Logger.LogInfo("LayerMask MoveItem:");
        for (var i = 0; i < 32; i++)
        {
            LethalShipSort.Logger.LogInfo(
                $" {((1073744640 & (1 << i)) != 0 ? "o" : "-")} {LayerMask.LayerToName(i)}"
            );
        }

        ChatCommandAPI.ChatCommandAPI.Print("layer masks printed to log");
        error = null!;
        return true;
    }
}
