using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbitera.Application;
using Orbitera.Domain;
using System.Collections.ObjectModel;
using System.IO;
using System.Security;

namespace Orbitera.App.ViewModels;

public sealed partial class CleanupPlannerViewModel : PageViewModel
{
    private readonly AnalyzeDevelopmentInsightsUseCase _analyzeInsights;
    private readonly StorageAnalysisSession _session;
    private readonly IStorageCapacityProvider _capacityProvider;
    private CancellationTokenSource? _refreshCancellation;
    private StorageCapacity? _capacity;

    [ObservableProperty]
    private ObservableCollection<CleanupCandidateDisplayItem> _candidates = [];

    [ObservableProperty]
    private CleanupPlan? _plan;

    [ObservableProperty]
    private CleanupPlanSimulation? _simulation;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Run Atlas before planning a cleanup.";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _estimatedSelectedBytesDisplay = "0 B";

    [ObservableProperty]
    private string _currentAvailableSpaceDisplay = "Unavailable";

    public CleanupPlannerViewModel(
        AnalyzeDevelopmentInsightsUseCase analyzeInsights,
        StorageAnalysisSession session,
        IStorageCapacityProvider capacityProvider)
        : base(
            "CLEANUP PLANNER",
            "Plan before cleaning",
            "Select rule-based candidates, review their risks, and simulate the space that could be recovered.",
            "Run an Atlas scan first. This planner is read-only and never deletes or changes files.")
    {
        _analyzeInsights = analyzeInsights;
        _session = session;
        _capacityProvider = capacityProvider;
    }

    public bool CanRefresh => !IsLoading;

    public bool CanCancel => IsLoading;

    public bool CanCreatePlan => !IsLoading && Candidates.Any(candidate => candidate.IsSelected);

    public bool HasCandidates => Candidates.Count > 0;

    public bool HasPlan => Plan is not null;

    public bool HasNoCandidates => !HasCandidates && !IsLoading;

    public string PlanEstimatedBytesDisplay =>
        Plan is null ? "—" : FormatSize(Plan.EstimatedRecoverableBytes);

    public string SimulationSummary
    {
        get
        {
            if (Simulation?.ProjectedAvailableBytes is long projected &&
                Simulation.AvailableBeforeBytes is long availableBefore)
            {
                return $"Available space would change from {FormatSize(availableBefore)} to {FormatSize(projected)}. This is a read-only estimate.";
            }

            return "Estimated recovery is based on scanned candidate sizes. Projected free space is unavailable because volume capacity could not be read.";
        }
    }

    public string EmptyStateMessage => HasCandidates
        ? string.Empty
        : "No rule-based development candidates are available. Run Atlas and ensure the scan includes the project folders.";

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        var cancellation = _refreshCancellation;
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Loading explainable candidates from the latest Atlas scan…";
        Plan = null;
        Simulation = null;
        Candidates.Clear();
        _capacity = null;
        CurrentAvailableSpaceDisplay = "Unavailable";

        try
        {
            var result = await Task.Run(
                () => _analyzeInsights.Execute(cancellation.Token),
                cancellation.Token);

            if (result is null)
            {
                StatusMessage = "Run an Atlas scan before creating a cleanup plan.";
                UpdateState();
                return;
            }

            var scanRoot = _session.LatestRoot;
            if (scanRoot is not null)
            {
                _capacity = await _capacityProvider.GetCapacityAsync(scanRoot.FullPath, cancellation.Token);
                CurrentAvailableSpaceDisplay = _capacity is null
                    ? "Unavailable"
                    : FormatSize(_capacity.AvailableBytes);
            }

            foreach (var recommendation in result.Recommendations)
            {
                var candidate = new CleanupCandidateDisplayItem(recommendation);
                candidate.PropertyChanged += OnCandidatePropertyChanged;
                Candidates.Add(candidate);
            }

            StatusMessage = Candidates.Count == 0
                ? "No project-linked candidates matched the configured rules."
                : $"{Candidates.Count:N0} candidates loaded. Select only items you have reviewed.";
            UpdateState();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage = "Candidate loading cancelled.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           SecurityException or ArgumentException or OverflowException)
        {
            ErrorMessage = $"Could not load cleanup candidates: {exception.Message}";
            StatusMessage = "Candidate loading could not be completed.";
        }
        finally
        {
            IsLoading = false;
            _refreshCancellation?.Dispose();
            _refreshCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelRefresh() => _refreshCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCreatePlan))]
    private void CreatePlan()
    {
        ErrorMessage = string.Empty;

        try
        {
            var plan = CleanupPlanFactory.Create(
                Candidates
                    .Where(candidate => candidate.IsSelected)
                    .Select(candidate => candidate.Recommendation),
                Candidates
                    .Where(candidate => candidate.IsSelected)
                    .Select(candidate => candidate.Path));
            Plan = plan;
            Simulation = CleanupPlanSimulator.Simulate(
                plan,
                _capacity?.TotalBytes,
                _capacity?.AvailableBytes);
            EstimatedSelectedBytesDisplay = FormatSize(plan.EstimatedRecoverableBytes);
            StatusMessage = $"Simulation ready: {plan.Items.Count:N0} selected item(s), {EstimatedSelectedBytesDisplay} estimated. No files were changed.";
            UpdateState();
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            ErrorMessage = $"Could not create the cleanup simulation: {exception.Message}";
        }
    }

    private void OnCandidatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupCandidateDisplayItem.IsSelected))
        {
            Plan = null;
            Simulation = null;
            EstimatedSelectedBytesDisplay = FormatSize(
                Candidates
                    .Where(candidate => candidate.IsSelected)
                    .Aggregate(0L, (total, candidate) => checked(total + candidate.SizeBytes)));
            UpdateState();
        }
    }

    private void UpdateState()
    {
        OnPropertyChanged(nameof(CanCreatePlan));
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(HasNoCandidates));
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(PlanEstimatedBytesDisplay));
        OnPropertyChanged(nameof(SimulationSummary));
        OnPropertyChanged(nameof(EmptyStateMessage));
        CreatePlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnPlanChanged(CleanupPlan? value)
    {
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(PlanEstimatedBytesDisplay));
        OnPropertyChanged(nameof(SimulationSummary));
    }

    partial void OnSimulationChanged(CleanupPlanSimulation? value)
    {
        OnPropertyChanged(nameof(SimulationSummary));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanCancel));
        RefreshCommand.NotifyCanExecuteChanged();
        CancelRefreshCommand.NotifyCanExecuteChanged();
        UpdateState();
    }

    private static string FormatSize(long sizeBytes)
    {
        var size = (double)sizeBytes;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}

public sealed partial class CleanupCandidateDisplayItem : ObservableObject
{
    internal DevelopmentInsightRecommendation Recommendation { get; }

    public CleanupCandidateDisplayItem(DevelopmentInsightRecommendation recommendation)
    {
        Recommendation = recommendation;
    }

    [ObservableProperty]
    private bool _isSelected;

    public string ProjectName => Recommendation.ProjectName;

    public string DirectoryName => Recommendation.DirectoryName;

    public string Path => Recommendation.DirectoryPath;

    public long SizeBytes => Recommendation.SizeBytes;

    public string SizeDisplay => FormatSize(SizeBytes);

    public string Ecosystem => Recommendation.Ecosystem switch
    {
        DevelopmentEcosystem.DotNet => ".NET",
        DevelopmentEcosystem.NodeJs => "Node.js",
        DevelopmentEcosystem.Python => "Python",
        _ => "Git"
    };

    public string Category => Recommendation.Category switch
    {
        DevelopmentInsightCategory.Dependency => "Dependency",
        DevelopmentInsightCategory.Cache => "Cache",
        DevelopmentInsightCategory.BuildArtifact => "Build artifact",
        _ => "Version control"
    };

    public string Risk => Recommendation.RiskLevel.ToString();

    public bool IsRecoverable => Recommendation.IsPotentiallyRecoverable;

    public string Purpose => Recommendation.Purpose;

    public string Impact => Recommendation.DeletionImpact;

    private static string FormatSize(long sizeBytes)
    {
        var size = (double)sizeBytes;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
