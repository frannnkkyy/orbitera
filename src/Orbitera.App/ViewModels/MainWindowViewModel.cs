using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Orbitera.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private PageViewModel _currentViewModel = null!;

    public MainWindowViewModel(
        OverviewViewModel overview,
        AtlasViewModel atlas,
        HistoryViewModel history,
        DevInsightsViewModel devInsights,
        CleanupPlannerViewModel cleanupPlanner,
        FileIntelligenceViewModel fileIntelligence,
        SettingsViewModel settings)
    {
        _currentViewModel = overview;

        NavigationItems =
        [
            new("overview", "Overview", "M3,10 L12,3 21,10 V20 H14 V14 H10 V20 H3 Z", overview, true),
            new("atlas", "Atlas", "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z", atlas),
            new("history", "History", "M4,12 A8,8 0 1 1 6,18 M4,12 V6 M4,12 H10 M12,7 V12 L16,14", history),
            new("dev-insights", "Dev Insights", "M8,5 H5 V19 H8 M16,5 H19 V19 H16 M14,9 L10,15", devInsights),
            new("cleanup-planner", "Cleanup Planner", "M12,3 L20,6 V11 C20,16 16,19 12,21 C8,19 4,16 4,11 V6 Z M9,12 L11,14 L15,10", cleanupPlanner),
            new("file-intelligence", "File Intelligence", "M6,3 H14 L19,8 V21 H6 Z M14,3 V8 H19 M9,13 H16 M9,17 H14", fileIntelligence),
            new("settings", "Settings", "M12,3 V6 M12,18 V21 M3,12 H6 M18,12 H21 M5.6,5.6 L7.7,7.7 M16.3,16.3 L18.4,18.4 M18.4,5.6 L16.3,7.7 M7.7,16.3 L5.6,18.4 M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12", settings)
        ];
    }

    [RelayCommand]
    private void Navigate(NavigationItemViewModel navigationItem)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, navigationItem);
        }

        CurrentViewModel = navigationItem.Page;
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
}
