using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbitera.Application;
using Orbitera.Domain;
using System.Collections.ObjectModel;
using System.IO;

namespace Orbitera.App.ViewModels;

public sealed partial class DevInsightsViewModel : PageViewModel
{
    private readonly AnalyzeDevelopmentInsightsUseCase _analyzeInsights;
    private CancellationTokenSource? _analysisCancellation;

    [ObservableProperty]
    private ObservableCollection<DetectedProjectDisplayItem> _projects = [];

    [ObservableProperty]
    private ObservableCollection<DevelopmentInsightDisplayItem> _recommendations = [];

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _hasAnalyzed;

    [ObservableProperty]
    private string _statusMessage = "Ejecuta un análisis de Atlas para consultar información de desarrollo.";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _scanRootPath = string.Empty;

    [ObservableProperty]
    private string _potentiallyRecoverableDisplay = "—";

    [ObservableProperty]
    private int _dotNetArtifactCount;

    [ObservableProperty]
    private int _nodeArtifactCount;

    [ObservableProperty]
    private int _pythonArtifactCount;

    [ObservableProperty]
    private int _gitArtifactCount;

    private readonly StorageAnalysisSession _session;

    public DevInsightsViewModel(AnalyzeDevelopmentInsightsUseCase analyzeInsights, StorageAnalysisSession session)
        : base(
            "DEV INSIGHTS",
            "Understand your development footprint.",
            "Recognize project dependencies, caches, and generated outputs with rules that explain their purpose and cleanup impact.",
            "Analyze a location in Atlas to identify development data.")
    {
        _analyzeInsights = analyzeInsights;
        _session = session;
    }

    public bool CanCancel => IsAnalyzing;

    public bool CanAnalyze => !IsAnalyzing;

    public string ScanRootDisplay => string.IsNullOrEmpty(ScanRootPath)
        ? "No Atlas scan available yet"
        : ScanRootPath;

    public bool HasProjects => Projects.Count > 0;

    public bool HasRecommendations => Recommendations.Count > 0;

    public string EmptyStateMessage => HasAnalyzed
        ? "No project-linked development folders matched the configured rules in this scan."
        : "Run an Atlas scan first. Dev Insights uses that local scan and does not scan or change files independently.";

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _analysisCancellation = new CancellationTokenSource();
        var cancellation = _analysisCancellation;
        IsAnalyzing = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Recognizing projects and classifying development folders…";

        try
        {
            var result = await Task.Run(
                () => _analyzeInsights.Execute(cancellation.Token),
                cancellation.Token);

            if (result is null)
            {
                HasAnalyzed = false;
                ScanRootPath = string.Empty;
                Projects.Clear();
                Recommendations.Clear();
                PotentiallyRecoverableDisplay = "—";
                StatusMessage = "Run an Atlas scan first to make development insights available.";
                UpdateSummary();
                return;
            }

            var root = _session.LatestRoot;
            ScanRootPath = root?.FullPath ?? string.Empty;
            Projects = new ObservableCollection<DetectedProjectDisplayItem>(
                result.Projects.Select(project => new DetectedProjectDisplayItem(project)));
            Recommendations = new ObservableCollection<DevelopmentInsightDisplayItem>(
                result.Recommendations.Select(item => new DevelopmentInsightDisplayItem(item)));
            PotentiallyRecoverableDisplay = FormatSize(result.PotentiallyRecoverableBytes);
            HasAnalyzed = true;
            StatusMessage = result.Projects.Count == 0
                ? "Analysis complete. No known project markers were found."
                : $"Analysis complete. Found {result.Projects.Count:N0} projects and {result.Recommendations.Count:N0} rule-based candidates.";
            UpdateSummary();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage = "Development insights analysis cancelled.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ErrorMessage = $"Could not analyze development insights: {exception.Message}";
            StatusMessage = "Analysis could not be completed.";
        }
        finally
        {
            IsAnalyzing = false;
            _analysisCancellation?.Dispose();
            _analysisCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelAnalysis() => _analysisCancellation?.Cancel();

    private void UpdateSummary()
    {
        DotNetArtifactCount = Recommendations.Count(item => item.Ecosystem == DevelopmentEcosystem.DotNet);
        NodeArtifactCount = Recommendations.Count(item => item.Ecosystem == DevelopmentEcosystem.NodeJs);
        PythonArtifactCount = Recommendations.Count(item => item.Ecosystem == DevelopmentEcosystem.Python);
        GitArtifactCount = Recommendations.Count(item => item.Ecosystem == DevelopmentEcosystem.Git);
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasRecommendations));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanAnalyze));
        CancelAnalysisCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
    }

    partial void OnScanRootPathChanged(string value) =>
        OnPropertyChanged(nameof(ScanRootDisplay));

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

public sealed class DetectedProjectDisplayItem(DetectedDevelopmentProject project)
{
    public string Name => project.Name;

    public string Path => project.RootPath;

    public string Ecosystems => string.Join(", ", project.Ecosystems.Select(FormatEcosystem));

    public string Indicators => string.Join(", ", project.Indicators);

    private static string FormatEcosystem(DevelopmentEcosystem ecosystem) => ecosystem switch
    {
        DevelopmentEcosystem.DotNet => ".NET",
        DevelopmentEcosystem.NodeJs => "Node.js",
        DevelopmentEcosystem.Python => "Python",
        _ => "Git"
    };
}

public sealed class DevelopmentInsightDisplayItem(DevelopmentInsightRecommendation recommendation)
{
    public string ProjectName => recommendation.ProjectName;

    public string ProjectPath => recommendation.ProjectPath;

    public string DirectoryName => recommendation.DirectoryName;

    public string DirectoryPath => recommendation.DirectoryPath;

    public long SizeBytes => recommendation.SizeBytes;

    public string SizeDisplay => FormatSize(recommendation.SizeBytes);

    public string Ecosystem => recommendation.Ecosystem switch
    {
        DevelopmentEcosystem.DotNet => ".NET",
        DevelopmentEcosystem.NodeJs => "Node.js",
        DevelopmentEcosystem.Python => "Python",
        _ => "Git"
    };

    public string Category => recommendation.Category switch
    {
        DevelopmentInsightCategory.Dependency => "Dependency",
        DevelopmentInsightCategory.Cache => "Cache",
        DevelopmentInsightCategory.BuildArtifact => "Build artifact",
        _ => "Version control"
    };

    public string Risk => recommendation.RiskLevel switch
    {
        DevelopmentRiskLevel.Low => "Low",
        DevelopmentRiskLevel.Moderate => "Moderate",
        _ => "High"
    };

    public string Recoverable => recommendation.IsPotentiallyRecoverable ? "Potential" : "No";

    public string Purpose => recommendation.Purpose;

    public string DeletionImpact => recommendation.DeletionImpact;

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
