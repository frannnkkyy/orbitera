using CommunityToolkit.Mvvm.ComponentModel;

namespace Orbitera.App.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(string sectionLabel, string title, string description, string placeholderMessage)
    {
        SectionLabel = sectionLabel;
        Title = title;
        Description = description;
        PlaceholderMessage = placeholderMessage;
    }

    public string SectionLabel { get; }

    public string Title { get; }

    public string Description { get; }

    public string PlaceholderMessage { get; }
}
