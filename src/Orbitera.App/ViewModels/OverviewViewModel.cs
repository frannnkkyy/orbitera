using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbitera.Application;
using Orbitera.Domain;
using System.Collections.ObjectModel;
using System.IO;
using System.Security;

namespace Orbitera.App.ViewModels;

public sealed partial class OverviewViewModel : PageViewModel
{
    private static readonly string[] AllFactorNames =
    [
        "Available space",
        "Growth trend",
        "Identified duplicates",
        "Regenerable data",
        "Changes between scans"
    ];

    private readonly CalculateOrbitScoreUseCase _calculateOrbitScore;
    private readonly StorageAnalysisSession _session;

    [ObservableProperty]
    private string _scoreDisplay = "—";

    [ObservableProperty]
    private string _scoreCoverageDisplay = "Awaiting an Atlas scan";

    [ObservableProperty]
    private string _scoreSummary = "Orbit Score describes storage usage signals, not physical disk health.";

    [ObservableProperty]
    private string _lastScanDisplay = "No Atlas scan yet";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private ObservableCollection<OrbitScoreFactorDisplayItem> _scoreFactors = [];

    [ObservableProperty]
    private ObservableCollection<string> _unavailableFactors = new(AllFactorNames);

    public OverviewViewModel(
        CalculateOrbitScoreUseCase calculateOrbitScore,
        StorageAnalysisSession session)
        : base(
            "YOUR STORAGE, IN A NEW LIGHT",
            "See your storage differently.",
            "Orbitera brings storage usage, change over time, and development data together in one clear view.",
            "Run an Atlas scan to start understanding this location.")
    {
        _calculateOrbitScore = calculateOrbitScore;
        _session = session;
    }

    public bool CanRefresh => !IsRefreshing;

    public string UnavailableFactorsDisplay => string.Join(", ", UnavailableFactors);

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _calculateOrbitScore.ExecuteAsync(CancellationToken.None);
            LastScanDisplay = _session.CapturedUtc?.ToLocalTime().ToString("g") ?? "No Atlas scan yet";

            if (result is null)
            {
                ScoreDisplay = "—";
                ScoreCoverageDisplay = "Not enough data for a score";
                ScoreSummary = "Run Atlas on a local volume to provide storage capacity and usage data.";
                ScoreFactors.Clear();
                UnavailableFactors = new ObservableCollection<string>(AllFactorNames);
                OnPropertyChanged(nameof(UnavailableFactorsDisplay));
                return;
            }

            ScoreDisplay = result.Score.ToString("0");
            ScoreCoverageDisplay = $"{result.CoveragePercent}% of factor weight available";
            ScoreSummary = result.CoveragePercent == 100
                ? "All configured factors contributed to this score. It is not a measurement of disk hardware health."
                : "This partial score uses only available evidence. Missing data is excluded, not treated as zero.";
            ScoreFactors = new ObservableCollection<OrbitScoreFactorDisplayItem>(
                result.Factors.Select(factor => new OrbitScoreFactorDisplayItem(factor)));
            var presentFactors = result.Factors.Select(factor => factor.Name).ToHashSet(StringComparer.Ordinal);
            UnavailableFactors = new ObservableCollection<string>(
                AllFactorNames.Where(name => !presentFactors.Contains(name)));
            OnPropertyChanged(nameof(UnavailableFactorsDisplay));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           SecurityException or ArgumentException or SnapshotPersistenceException or
                                           OverflowException)
        {
            ErrorMessage = $"Orbit Score could not be calculated: {exception.Message}";
            ScoreDisplay = "—";
            ScoreCoverageDisplay = "Calculation unavailable";
            ScoreSummary = "The score was not calculated because one or more data sources could not be read.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    partial void OnIsRefreshingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        RefreshCommand.NotifyCanExecuteChanged();
    }
}

public sealed class OrbitScoreFactorDisplayItem(OrbitScoreFactor factor)
{
    public string Name => factor.Name;

    public int Score => factor.Score;

    public int Weight => factor.Weight;

    public string Explanation => factor.Explanation;
}
