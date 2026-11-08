namespace Orbitera.App.ViewModels;

public sealed class FileIntelligenceViewModel : PageViewModel
{
    public FileIntelligenceViewModel()
        : base(
            "FILE INTELLIGENCE",
            "Find the files worth understanding.",
            "Surface large, old, and duplicate files with enough context to make informed decisions.",
            "An analysis is needed before Orbitera can identify files on this device.")
    {
    }
}
