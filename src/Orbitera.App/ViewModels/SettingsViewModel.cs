namespace Orbitera.App.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    public SettingsViewModel()
        : base(
            "SETTINGS",
            "Make Orbitera yours.",
            "A few thoughtful preferences will shape how you explore your storage.",
            "Settings for future analyses will be available here.")
    {
    }
}
