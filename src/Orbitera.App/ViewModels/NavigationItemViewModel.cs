using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Orbitera.App.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public NavigationItemViewModel(
        string id,
        string title,
        string iconPath,
        PageViewModel page,
        bool isSelected = false)
    {
        Id = id;
        Title = title;
        Icon = Geometry.Parse(iconPath);
        Page = page;
        _isSelected = isSelected;
    }

    public string Id { get; }

    public string Title { get; }

    public Geometry Icon { get; }

    public PageViewModel Page { get; }
}
