namespace Orbitera.App.ViewModels;

public sealed class HistoryViewModel : PageViewModel
{
    public HistoryViewModel()
        : base(
            "ORBIT HISTORY",
            "Understand what changed",
            "Compare storage snapshots to see which files appeared, disappeared, or changed in size.",
            "Historical comparisons will be available after snapshot storage is implemented.")
    {
    }
}
