using System;
using System.IO;
using System.Text;
using ChatCommandAPI;
using ChatCommandAPI.Utils;

namespace LethalShipSort.Commands;

public class SortStatusCommand : Command
{
    public override string Name => "ShipSortStatus";

    public override string Description => "Displays some information about ShipSort";

    public override string Command => "sort-status";

    public readonly struct PreviousSort
    {
        internal PreviousSort(
            DateTime utcNow,
            TimeSpan duration,
            uint sorted,
            uint total,
            bool autosort,
            string scriptName
        )
        {
            Time = utcNow;
            Duration = duration;
            Sorted = sorted;
            Total = total;
            WasAutoSort = autosort;
            ScriptName = scriptName;
        }

        public readonly DateTime Time;
        public readonly TimeSpan Duration;
        public readonly uint Sorted;
        public readonly uint Total;
        public readonly bool WasAutoSort;
        public readonly string ScriptName;

        public override string ToString()
        {
            return $"{(WasAutoSort ? "Autosorted" : "Sorted")} {(DateTime.UtcNow - Time).ToReadableString()} ago ({Sorted}/{Total} items in {Duration.ToReadableString()}) using {ScriptName}";
        }
    }

    public static PreviousSort? PrevSort { get; internal set; }

    public override void Invoke(string args)
    {
        var sb = new StringBuilder();

        var prevSort = PrevSort;
        if (prevSort != null)
        {
            sb.Append(prevSort.ToString() + "\n");
        }

        var mod = LethalShipSort.Instance;
        sb.Append(
            $"Current script: {(mod.UseSharedConfig ? "[SHARED]" : Path.GetFileName(mod.ScriptPath))}"
        );
        Chat.Print(sb.ToString());
    }
}
