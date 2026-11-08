namespace Orbitera.App.ViewModels;

public sealed class DevInsightsViewModel : PageViewModel
{
    public DevInsightsViewModel()
        : base(
            "DEV INSIGHTS",
            "Recognize development data",
            "Identify projects, dependencies, caches, and generated files with transparent rules.",
            "Development-file detection is not active in this initial project structure.")
    {
    }
}
