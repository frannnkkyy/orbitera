using Orbitera.Application;
using Orbitera.Domain;
using Orbitera.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Orbitera.Infrastructure.Tests;

public sealed class FileSystemStorageScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        $"OrbiteraScannerTests-{Guid.NewGuid():N}");

    public FileSystemStorageScannerTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ScanAsyncBuildsHierarchyAndAccumulatesDirectorySizes()
    {
        var nestedPath = Directory.CreateDirectory(Path.Combine(_rootPath, "nested")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "readme.txt"), [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(Path.Combine(nestedPath, "picture.png"), [5, 6, 7]);
        var progress = new InlineProgress<StorageScanProgress>();

        var result = await new FileSystemStorageScanner()
            .ScanAsync(_rootPath, progress, CancellationToken.None);

        Assert.Equal(2, result.FilesScanned);
        Assert.Equal(2, result.DirectoriesScanned);
        Assert.Equal(0, result.InaccessibleEntries);
        Assert.Equal(0, result.SkippedReparsePoints);
        Assert.Equal(7, result.Root.SizeBytes);
        Assert.Equal(3, result.Root.Children.Single(child => child.Name == "nested").SizeBytes);
        Assert.Equal(".txt", result.Root.Children.Single(child => child.Name == "readme.txt").Extension);
        Assert.Equal(".png", result.Root.Children.Single(child => child.Name == "nested")
            .Children.Single().Extension);
        Assert.Equal(2, progress.LastProgress?.FilesScanned);
        Assert.True(result.Root.LastModifiedUtc.HasValue);
    }

    [Fact]
    public async Task ScanAsyncHonorsCancellationBeforeStarting()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileSystemStorageScanner().ScanAsync(
                _rootPath,
                progress: null,
                cancellation.Token));
    }

    [Fact]
    public async Task ScanAsyncReportsMissingRootDirectory()
    {
        var missingPath = Path.Combine(_rootPath, "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new FileSystemStorageScanner().ScanAsync(
                missingPath,
                progress: null,
                CancellationToken.None));
    }

    [Fact]
    public async Task SqliteRepositoryPersistsSnapshotMetadataAndRelativeFileEntries()
    {
        var nestedPath = Directory.CreateDirectory(Path.Combine(_rootPath, "nested")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(nestedPath, "data.bin"), [1, 2, 3, 4, 5]);
        var scan = await new FileSystemStorageScanner()
            .ScanAsync(_rootPath, progress: null, CancellationToken.None);
        var databasePath = Path.Combine(_rootPath, "history", "orbitera.db");
        var repository = new SqliteSnapshotRepository(databasePath);
        var capturedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var summary = await repository.SaveAsync(scan.Root, capturedUtc, isComplete: true, CancellationToken.None);
        var currentRoot = StorageNode.CreateDirectory(_rootPath, _rootPath, lastModifiedUtc: capturedUtc.AddDays(1));
        var currentNested = StorageNode.CreateDirectory("nested", Path.Combine(_rootPath, "nested"));
        currentNested.AddChild(StorageNode.CreateFile(
            "data.bin",
            Path.Combine(_rootPath, "nested", "data.bin"),
            9,
            ".bin"));
        currentRoot.AddChild(currentNested);
        var currentSummary = await repository.SaveAsync(
            currentRoot,
            capturedUtc.AddDays(1),
            isComplete: true,
            CancellationToken.None);
        var roots = await repository.GetRootsAsync(CancellationToken.None);
        var snapshots = await repository.GetSnapshotsAsync(_rootPath, CancellationToken.None);
        var saved = await repository.GetSnapshotAsync(summary.Id, CancellationToken.None);
        var comparison = await repository.CompareAsync(summary.Id, currentSummary.Id, CancellationToken.None);

        Assert.Single(roots);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(capturedUtc, saved.Summary.CapturedUtc);
        Assert.Equal(1, saved.Summary.FileCount);
        Assert.Equal(5, saved.Summary.TotalSizeBytes);
        Assert.Equal(@"nested\data.bin", Assert.Single(saved.Files).RelativePath);
        Assert.Single(comparison.ResizedFiles);
        Assert.Equal(4, comparison.NetGrowthBytes);
        Assert.Equal(4, comparison.FolderGrowths.Single(folder => folder.RelativePath == "nested").NetGrowthBytes);
    }

    [Fact]
    public async Task SqliteRepositoryRejectsComparisonWithIncompleteSnapshot()
    {
        var root = StorageNode.CreateDirectory(_rootPath, _rootPath);
        var repository = new SqliteSnapshotRepository(Path.Combine(_rootPath, "history", "orbitera.db"));
        var capturedUtc = DateTime.UtcNow;
        var partial = await repository.SaveAsync(root, capturedUtc, isComplete: false, CancellationToken.None);
        var complete = await repository.SaveAsync(root, capturedUtc.AddMinutes(1), isComplete: true, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.CompareAsync(partial.Id, complete.Id, CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        public T? LastProgress { get; private set; }

        public void Report(T value) => LastProgress = value;
    }
}
