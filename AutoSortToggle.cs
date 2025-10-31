using ChatCommandAPI;

namespace LethalShipSort;

public class AutoSortToggle : ToggleCommand
{
    public override string Name => "AutoSort";
    public override string ToggleDescription =>
        "Toggles automatic item sorting when leaving a planet";

    public override bool Value
    {
        get => LethalShipSort.Instance.AutoSort;
        set => LethalShipSort.Instance.AutoSort = value;
    }
}
