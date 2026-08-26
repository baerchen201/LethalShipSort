#if DEBUG
using System.Linq;
using System.Runtime.CompilerServices;
using ChatCommandAPI;
using ChatCommandAPI.Utils;
using UnityEngine;

namespace LethalShipSort.Commands;

public class GenMDTablesCommand : Command
{
    public override string Name => "GenerateMarkdownTables";

    public override string[] Aliases => ["GenerateSortTables"];

    public override string Description =>
        "Creates markdown-formatted tables for all items and all moons";

    public override void Invoke(string args)
    {
        LethalShipSort.Logger.LogInfo(
            Utils.GenerateMDTable(
                Resources.FindObjectsOfTypeAll<SelectableLevel>(),
                ("ID", i => i.levelID),
                ("Name", i => NoneIfEmpty($"`{i.PlanetName}`")),
                ("Scene", i => NoneIfEmpty($"`{i.sceneName}`"))
            )
        );
        var objects = Object.FindObjectsByType<GrabbableObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        LethalShipSort.Logger.LogInfo(
            Utils.GenerateMDTable(
                Resources
                    .FindObjectsOfTypeAll<GrabbableObject>()
                    .Where(i => !objects.Contains(i))
                    .ToArray(),
                ("Name", i => NoneIfEmpty($"`{SortAPI.ItemName(i)}`")),
                ("Type", i => NoneIfEmpty($"`{i.GetType().Name}`")),
                ("Scrap", i => SortAPI.ItemScrap(i).ToString()),
                ("Large", i => SortAPI.ItemLarge(i).ToString()),
                ("Argument", i => SortAPI.ItemArg(i).TypeToString())
            )
        );

        Chat.Print("Generated tables");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NoneIfEmpty(string str)
    {
        return str.Length > 2 ? str : "_none_";
    }
}
#endif
