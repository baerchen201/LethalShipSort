#if DEBUG
using ChatCommandAPI;

namespace LethalShipSort.Commands;

public class SortHelperCommand : MultiOptionCommand<SortAPI.TRANSFORM>
{
    public static SortAPI.TRANSFORM SortHelper;

    public override string Name => "SortHelper";

    public override string Description => "Displays item drop positions for creating scripts";

    public override SortAPI.TRANSFORM CurrentValue
    {
        get => SortHelper;
        set => SortHelper = value;
    }
}
#endif
