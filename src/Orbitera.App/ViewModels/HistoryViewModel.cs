using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbitera.Application;
using Orbitera.Domain;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Orbitera.App.ViewModels;

public sealed partial class HistoryViewModel : PageViewModel
{
    private const double ChartWidth = 720;
    private const double ChartHeight = 210;
    private readonly ISnapshotRepository _repository;
    private readonly CompareStorageSnapshotsUseCase _compareSnapshots;

    [ObservableProperty]
    private ObservableCollection<SnapshotRoot> _roots = [];

    [ObservableProperty]
    private SnapshotRoot? _selectedRoot;

    [ObservableProperty]
    private ObservableCollection<SnapshotDisplayItem> _snapshots = [];

    [ObservableProperty]
    private SnapshotDisplayItem? _selectedPreviousSnapshot;

    [ObservableProperty]
    private SnapshotDisplayItem? _selectedCurrentSnapshot;

    [ObservableProperty]
    private SnapshotComparison? _comparison;

    [ObservableProperty]
    private ObservableCollection<FolderGrowthDisplayItem> _topGrowingFolders = [];

    [ObservableProperty]
    private ObservableCollection<FileChangeDisplayItem> _fileChanges = [];

    [ObservableProperty]
    private PointCollection _trendLinePoints = [];

    [ObservableProperty]
    private string _statusMessage = "Aún no hay snapshots guardados.";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasSnapshots;

    [ObservableProperty]
    private bool _hasTrend;

    [ObservableProperty]
    private bool _hasComparison;

    [ObservableProperty]
    private string _netGrowthDisplay = "—";

    [ObservableProperty]
    private string _addedCountDisplay = "0";

    [ObservableProperty]
    private string _removedCountDisplay = "0";

    [ObservableProperty]
    private string _resizedCountDisplay = "0";

    private bool _refreshingRoots;

    public HistoryViewModel(
        ISnapshotRepository repository,
        CompareStorageSnapshotsUseCase compareSnapshots)
        : base(
            "ORBIT HISTORY",
            "See how your storage changes over time.",
            "Compare saved snapshots to understand which files appeared, disappeared, or changed size—and which folders are growing.",
            "Run an Atlas scan to start building your local storage history.")
    {
        _repository = repository;
        _compareSnapshots = compareSnapshots;
        _ = RefreshCommand.ExecuteAsync(null);
    }

    public bool CanCompare =>
        !IsBusy &&
        SelectedPreviousSnapshot is not null &&
        SelectedCurrentSnapshot is not null &&
        SelectedPreviousSnapshot.Summary.Id != SelectedCurrentSnapshot.Summary.Id;

    public string ComparisonLabel =>
        Comparison is null
            ? "Elige dos snapshots para compararlos."
            : $"{Comparison.Previous.CapturedUtc.ToLocalTime():g}  →  {Comparison.Current.CapturedUtc.ToLocalTime():g}";

    public string TrendEmptyMessage => Snapshots.Count switch
    {
        0 => "Cuando Atlas guarde un análisis, aparecerá aquí.",
        1 => "Un segundo snapshot permitirá mostrar la evolución.",
        _ => "No hay datos suficientes para dibujar la tendencia."
    };

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        _refreshingRoots = true;

        try
        {
            var currentPath = SelectedRoot?.Path;
            var roots = await _repository.GetRootsAsync(CancellationToken.None);
            Roots = new ObservableCollection<SnapshotRoot>(roots);
            SelectedRoot = Roots.FirstOrDefault(root =>
                string.Equals(root.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                ?? Roots.FirstOrDefault();
            await LoadSnapshotsAsync();
        }
        catch (SnapshotPersistenceException exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "No se pudo cargar el historial local.";
        }
        finally
        {
            _refreshingRoots = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadSnapshotsAsync()
    {
        if (SelectedRoot is null)
        {
            Snapshots.Clear();
            ResetComparison();
            UpdateSnapshotState();
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var summaries = await _repository.GetSnapshotsAsync(SelectedRoot.Path, CancellationToken.None);
            Snapshots = new ObservableCollection<SnapshotDisplayItem>(
                summaries.Select(summary => new SnapshotDisplayItem(summary)));
            SelectedCurrentSnapshot = Snapshots.FirstOrDefault();
            SelectedPreviousSnapshot = Snapshots.Skip(1).FirstOrDefault();
            BuildTrend(summaries);
            UpdateSnapshotState();

            if (SelectedPreviousSnapshot is not null &&
                SelectedCurrentSnapshot is not null &&
                SelectedPreviousSnapshot.Summary.Id != SelectedCurrentSnapshot.Summary.Id)
            {
                await CompareAsync();
            }
            else
            {
                ResetComparison();
                StatusMessage = summaries.Count == 0
                    ? "Aún no hay snapshots guardados para esta ubicación."
                    : "Se necesita otro snapshot de esta ubicación para comparar.";
            }
        }
        catch (SnapshotPersistenceException exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "No se pudo cargar el historial local.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanCompare));
            CompareCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync()
    {
        if (SelectedPreviousSnapshot is null || SelectedCurrentSnapshot is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            Comparison = await _compareSnapshots.ExecuteAsync(
                SelectedPreviousSnapshot.Summary.Id,
                SelectedCurrentSnapshot.Summary.Id,
                CancellationToken.None);
            UpdateComparisonDisplay();
            StatusMessage = Comparison.AddedFiles.Count == 0 &&
                            Comparison.RemovedFiles.Count == 0 &&
                            Comparison.ResizedFiles.Count == 0
                ? "No se encontraron cambios entre estos snapshots."
                : "Comparación completada.";
        }
        catch (SnapshotPersistenceException exception)
        {
            ErrorMessage = exception.Message;
            ResetComparison();
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
            ResetComparison();
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
            ResetComparison();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanCompare));
            CompareCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedRootChanged(SnapshotRoot? value)
    {
        if (!_refreshingRoots)
        {
            _ = LoadSnapshotsCommand.ExecuteAsync(null);
        }
    }

    partial void OnSelectedPreviousSnapshotChanged(SnapshotDisplayItem? value)
    {
        OnPropertyChanged(nameof(CanCompare));
        CompareCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCurrentSnapshotChanged(SnapshotDisplayItem? value)
    {
        OnPropertyChanged(nameof(CanCompare));
        CompareCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCompare));
        CompareCommand.NotifyCanExecuteChanged();
    }

    private void BuildTrend(IReadOnlyList<StorageSnapshotSummary> summaries)
    {
        if (summaries.Count < 2)
        {
            TrendLinePoints = [];
            HasTrend = false;
            OnPropertyChanged(nameof(TrendEmptyMessage));
            return;
        }

        var points = summaries
            .OrderBy(summary => summary.CapturedUtc)
            .ToArray();
        var minimum = points.Min(point => (double)point.TotalSizeBytes);
        var maximum = points.Max(point => (double)point.TotalSizeBytes);
        var sizeRange = Math.Max(1, maximum - minimum);
        var timeRange = Math.Max(
            1,
            (points[^1].CapturedUtc - points[0].CapturedUtc).Ticks);
        var mappedPoints = new PointCollection(points.Length);

        for (var index = 0; index < points.Length; index++)
        {
            var x = (points[index].CapturedUtc - points[0].CapturedUtc).Ticks /
                    (double)timeRange * ChartWidth;
            var y = ChartHeight - (points[index].TotalSizeBytes - minimum) / sizeRange * ChartHeight;
            mappedPoints.Add(new Point(x, y));
        }

        TrendLinePoints = mappedPoints;
        HasTrend = true;
        OnPropertyChanged(nameof(TrendEmptyMessage));
    }

    private void UpdateSnapshotState()
    {
        HasSnapshots = Snapshots.Count > 0;
        OnPropertyChanged(nameof(TrendEmptyMessage));
    }

    private void UpdateComparisonDisplay()
    {
        if (Comparison is null)
        {
            ResetComparison();
            return;
        }

        HasComparison = true;
        NetGrowthDisplay = FormatSignedSize(Comparison.NetGrowthBytes);
        AddedCountDisplay = Comparison.AddedFiles.Count.ToString("N0", CultureInfo.CurrentCulture);
        RemovedCountDisplay = Comparison.RemovedFiles.Count.ToString("N0", CultureInfo.CurrentCulture);
        ResizedCountDisplay = Comparison.ResizedFiles.Count.ToString("N0", CultureInfo.CurrentCulture);
        TopGrowingFolders = new ObservableCollection<FolderGrowthDisplayItem>(
            Comparison.FolderGrowths
                .Where(folder => folder.NetGrowthBytes > 0 && folder.RelativePath.Length > 0)
                .Take(8)
                .Select(folder => new FolderGrowthDisplayItem(folder)));

        FileChanges = new ObservableCollection<FileChangeDisplayItem>(
            Comparison.AddedFiles
                .Concat(Comparison.RemovedFiles)
                .Concat(Comparison.ResizedFiles)
                .OrderByDescending(change => Math.Abs(change.SizeDeltaBytes))
                .Take(500)
                .Select(change => new FileChangeDisplayItem(change)));

        OnPropertyChanged(nameof(ComparisonLabel));
    }

    private void ResetComparison()
    {
        Comparison = null;
        HasComparison = false;
        NetGrowthDisplay = "—";
        AddedCountDisplay = "0";
        RemovedCountDisplay = "0";
        ResizedCountDisplay = "0";
        TopGrowingFolders.Clear();
        FileChanges.Clear();
        OnPropertyChanged(nameof(ComparisonLabel));
    }

    private static string FormatSignedSize(long value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "−" : string.Empty;
        var absolute = Math.Abs((double)value);
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;

        while (absolute >= 1024 && unit < units.Length - 1)
        {
            absolute /= 1024;
            unit++;
        }

        return $"{sign}{absolute:0.##} {units[unit]}";
    }
}

public sealed class SnapshotDisplayItem(StorageSnapshotSummary summary)
{
    public StorageSnapshotSummary Summary { get; } = summary;

    public string Label =>
        $"{Summary.CapturedUtc.ToLocalTime():g} · {FormatSize(Summary.TotalSizeBytes)}" +
        (Summary.IsComplete ? string.Empty : " · Parcial");

    public override string ToString() => Label;

    private static string FormatSize(long value)
    {
        var size = (double)value;
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

public sealed class FolderGrowthDisplayItem(FolderGrowth growth)
{
    public string FolderName => growth.RelativePath;

    public long NetGrowthBytes => growth.NetGrowthBytes;

    public string GrowthDisplay => FormatSize(growth.NetGrowthBytes);

    public string Insight =>
        $"{growth.RelativePath} aumentó en {FormatSize(growth.NetGrowthBytes)} desde el análisis anterior.";

    private static string FormatSize(long value)
    {
        var size = (double)value;
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

public sealed class FileChangeDisplayItem(SnapshotFileChange change)
{
    public string RelativePath => change.RelativePath;

    public string ChangeType => change.Kind switch
    {
        SnapshotFileChangeKind.Added => "Nuevo",
        SnapshotFileChangeKind.Removed => "Eliminado",
        _ => "Tamaño cambiado"
    };

    public string PreviousSize => change.PreviousSizeBytes is { } size ? FormatSize(size) : "—";

    public string CurrentSize => change.CurrentSizeBytes is { } size ? FormatSize(size) : "—";

    public string Delta => FormatSignedSize(change.SizeDeltaBytes);

    private static string FormatSignedSize(long value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "−" : string.Empty;
        return $"{sign}{FormatSize(Math.Abs((double)value))}";
    }

    private static string FormatSize(double value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
