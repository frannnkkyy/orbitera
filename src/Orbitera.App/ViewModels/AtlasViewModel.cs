using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbitera.Application;
using Orbitera.Domain;
using Orbitera.App.Services;
using System.Collections.ObjectModel;
using System.Security;
using System.Windows.Media;

namespace Orbitera.App.ViewModels;

public partial class AtlasViewModel : PageViewModel
{
    private const double TreemapWidth = 1000;
    private const double TreemapHeight = 520;
    private readonly ScanStorageUseCase _scanStorage;
    private readonly IFolderPickerService _folderPicker;
    private CancellationTokenSource? _scanCancellation;

    [ObservableProperty]
    private ObservableCollection<StorageVolume> _volumes = [];

    [ObservableProperty]
    private StorageVolume? _selectedVolume;

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isLoadingVolumes;

    [ObservableProperty]
    private string _statusMessage = "Selecciona una unidad o una carpeta para comenzar.";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _progressPath = string.Empty;

    [ObservableProperty]
    private long _filesScanned;

    [ObservableProperty]
    private long _directoriesScanned;

    [ObservableProperty]
    private long _bytesScanned;

    [ObservableProperty]
    private int _inaccessibleEntries;

    [ObservableProperty]
    private int _skippedReparsePoints;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private StorageScanResult? _scanResult;

    [ObservableProperty]
    private AtlasItemViewModel? _selectedItem;

    public AtlasViewModel(ScanStorageUseCase scanStorage, IFolderPickerService folderPicker)
        : base(
            "ATLAS",
            "Una vista distinta de tu almacenamiento.",
            "Explora la estructura de carpetas y descubre qué ocupa espacio. Los resultados se calculan localmente y la superficie de cada rectángulo es proporcional a su tamaño.",
            "Selecciona una unidad o carpeta para generar tu primer mapa.")
    {
        _scanStorage = scanStorage;
        _folderPicker = folderPicker;
        _ = LoadVolumesCommand.ExecuteAsync(null);
    }

    public ObservableCollection<AtlasItemViewModel> CurrentItems { get; } = [];

    public ObservableCollection<AtlasItemViewModel> TreemapItems { get; } = [];

    public bool HasScanResult => ScanResult is not null;

    public bool CanNavigateUp => CurrentDirectory?.Parent is not null;

    public string CurrentDirectoryLabel =>
        CurrentDirectory is null
            ? "Sin ubicación"
            : CurrentDirectory.FullPath;

    private StorageNode? CurrentDirectory { get; set; }

    [RelayCommand]
    private async Task LoadVolumesAsync()
    {
        IsLoadingVolumes = true;
        ErrorMessage = string.Empty;

        try
        {
            var volumes = await Task.Run(_scanStorage.GetAvailableVolumes);
            Volumes = new ObservableCollection<StorageVolume>(volumes);

            if (Volumes.Count == 0)
            {
                StatusMessage = "No se encontraron unidades disponibles.";
            }
        }
        catch (Exception exception) when (IsExpectedScanException(exception))
        {
            ErrorMessage = $"No se pudieron consultar las unidades: {exception.Message}";
        }
        finally
        {
            IsLoadingVolumes = false;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var selectedFolder = _folderPicker.PickFolder(RootPath);

        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            SelectedVolume = null;
            RootPath = selectedFolder;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        ErrorMessage = string.Empty;
        ScanResult = null;
        CurrentItems.Clear();
        TreemapItems.Clear();
        CurrentDirectory = null;
        OnPropertyChanged(nameof(CanNavigateUp));
        OnPropertyChanged(nameof(CurrentDirectoryLabel));
        FilesScanned = 0;
        DirectoriesScanned = 0;
        BytesScanned = 0;
        InaccessibleEntries = 0;
        SkippedReparsePoints = 0;
        ProgressValue = 0;
        StatusMessage = "Preparando el análisis…";
        IsScanning = true;

        using var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        try
        {
            var progress = new Progress<StorageScanProgress>(UpdateProgress);
            var result = await _scanStorage.ExecuteAsync(
                RootPath,
                progress,
                cancellation.Token);

            ScanResult = result;
            CurrentDirectory = result.Root;
            CurrentPath = result.Root.FullPath;
            UpdateVisibleEntries();
            ProgressValue = 100;
            StatusMessage = $"Análisis completado en {result.Duration.TotalSeconds:F1} s.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage = "Análisis cancelado. No se modificó ningún archivo.";
        }
        catch (Exception exception) when (IsExpectedScanException(exception))
        {
            ErrorMessage = $"No se pudo analizar la ubicación: {exception.Message}";
            StatusMessage = "El análisis no pudo completarse.";
        }
        finally
        {
            IsScanning = false;
            _scanCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void CancelScan() => _scanCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanNavigateUp))]
    private void NavigateUp()
    {
        if (CurrentDirectory?.Parent is not { } parent)
        {
            return;
        }

        CurrentDirectory = parent;
        CurrentPath = parent.FullPath;
        SelectedItem = null;
        UpdateVisibleEntries();
        OnPropertyChanged(nameof(CanNavigateUp));
        NavigateUpCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedItem))]
    private void OpenSelectedFolder()
    {
        if (SelectedItem?.Node is not { IsDirectory: true } directory)
        {
            return;
        }

        CurrentDirectory = directory;
        CurrentPath = directory.FullPath;
        SelectedItem = null;
        UpdateVisibleEntries();
        OnPropertyChanged(nameof(CanNavigateUp));
        NavigateUpCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectTreemapItem(AtlasItemViewModel item)
    {
        SelectedItem = item;
    }

    private bool CanScan() => !IsScanning && !string.IsNullOrWhiteSpace(RootPath);

    private bool CanOpenSelectedItem() => SelectedItem?.Node.IsDirectory == true;

    private void UpdateProgress(StorageScanProgress progress)
    {
        FilesScanned = progress.FilesScanned;
        DirectoriesScanned = progress.DirectoriesScanned;
        BytesScanned = progress.BytesScanned;
        InaccessibleEntries = progress.InaccessibleEntries;
        SkippedReparsePoints = progress.SkippedReparsePoints;
        ProgressPath = progress.CurrentPath;
        StatusMessage = $"Analizando {Path.GetFileName(progress.CurrentPath)}";
        ProgressValue = 0;
    }

    private void UpdateVisibleEntries()
    {
        CurrentItems.Clear();
        TreemapItems.Clear();

        if (CurrentDirectory is null)
        {
            return;
        }

        foreach (var child in CurrentDirectory.Children
                     .OrderByDescending(node => node.SizeBytes)
                     .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new AtlasItemViewModel(child);
            CurrentItems.Add(item);
        }

        var itemsByNode = CurrentItems.ToDictionary(item => item.Node);
        var layout = SquarifiedTreemapLayout.Layout(
            CurrentDirectory.Children,
            new TreemapBounds(0, 0, TreemapWidth, TreemapHeight));

        foreach (var layoutItem in layout)
        {
            var item = itemsByNode[layoutItem.Node];
            item.SetBounds(layoutItem.Bounds);
            TreemapItems.Add(item);
        }
    }

    private static bool IsExpectedScanException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or SecurityException or
            ArgumentException or NotSupportedException;

    partial void OnRootPathChanged(string value) => ScanCommand.NotifyCanExecuteChanged();

    partial void OnIsScanningChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        CancelScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedVolumeChanged(StorageVolume? value)
    {
        if (value is not null)
        {
            RootPath = value.RootPath;
        }
    }

    partial void OnSelectedItemChanged(AtlasItemViewModel? value)
    {
        foreach (var item in CurrentItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        OpenSelectedFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnScanResultChanged(StorageScanResult? value) =>
        OnPropertyChanged(nameof(HasScanResult));
}

public sealed partial class AtlasItemViewModel : ObservableObject
{
    private static readonly IReadOnlyDictionary<StorageCategory, Brush> CategoryBrushes =
        new Dictionary<StorageCategory, Brush>
        {
            [StorageCategory.Folder] = CreateBrush("#385889"),
            [StorageCategory.Development] = CreateBrush("#7957C8"),
            [StorageCategory.Image] = CreateBrush("#3974B7"),
            [StorageCategory.Video] = CreateBrush("#8B54B9"),
            [StorageCategory.Audio] = CreateBrush("#347F91"),
            [StorageCategory.Archive] = CreateBrush("#9A6A39"),
            [StorageCategory.Document] = CreateBrush("#4C7E62"),
            [StorageCategory.Application] = CreateBrush("#9E4F68"),
            [StorageCategory.Other] = CreateBrush("#526174")
        };

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    public AtlasItemViewModel(StorageNode node)
    {
        Node = node;
        Category = StorageCategoryClassifier.Classify(node);
        CategoryBrush = CategoryBrushes[Category];
    }

    public StorageNode Node { get; }

    public string Name => Node.Name;

    public string FullPath => Node.FullPath;

    public string Type => Node.IsDirectory ? "Carpeta" : "Archivo";

    public string Extension => Node.IsDirectory
        ? "—"
        : string.IsNullOrWhiteSpace(Node.Extension) ? "Sin extensión" : Node.Extension;

    public long SizeBytes => Node.SizeBytes;

    public string SizeDisplay => FormatSize(Node.SizeBytes);

    public string ModifiedDisplay => Node.LastModifiedUtc?.ToLocalTime().ToString("g") ?? "Desconocida";

    public StorageCategory Category { get; }

    public string CategoryDisplay => Category switch
    {
        StorageCategory.Folder => "Carpeta",
        StorageCategory.Development => "Desarrollo",
        StorageCategory.Image => "Imagen",
        StorageCategory.Video => "Vídeo",
        StorageCategory.Audio => "Audio",
        StorageCategory.Archive => "Archivo comprimido",
        StorageCategory.Document => "Documento",
        StorageCategory.Application => "Instalador",
        _ => "Otros"
    };

    public Brush CategoryBrush { get; }

    public bool ShowLabel => Width >= 100 && Height >= 46;

    public Thickness SelectionBorderThickness => IsSelected ? new Thickness(2) : new Thickness(0);

    public Brush SelectionBorderBrush => IsSelected ? Brushes.White : Brushes.Transparent;

    public void SetBounds(TreemapBounds bounds)
    {
        X = bounds.X;
        Y = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    partial void OnWidthChanged(double value) => OnPropertyChanged(nameof(ShowLabel));

    partial void OnHeightChanged(double value) => OnPropertyChanged(nameof(ShowLabel));

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBorderThickness));
        OnPropertyChanged(nameof(SelectionBorderBrush));
    }

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static string FormatSize(long sizeBytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)sizeBytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
