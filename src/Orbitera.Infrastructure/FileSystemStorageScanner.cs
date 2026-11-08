using Orbitera.Application;
using Orbitera.Domain;
using System.Diagnostics;
using System.Security;

namespace Orbitera.Infrastructure;

public sealed class FileSystemStorageScanner : IStorageScanner
{
    private const int ProgressEntryInterval = 128;
    private static readonly TimeSpan ProgressTimeInterval = TimeSpan.FromMilliseconds(150);

    public IReadOnlyList<StorageVolume> GetAvailableVolumes()
    {
        return DriveInfo.GetDrives()
            .Select(drive =>
            {
                var isReady = TryGetReadyState(drive);
                var rootPath = drive.RootDirectory.FullName;
                var name = $"{drive.Name.TrimEnd('\\')} · {drive.DriveType}";

                return new StorageVolume(name, rootPath, isReady);
            })
            .ToArray();
    }

    public Task<StorageScanResult> ScanAsync(
        string rootPath,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () => ScanCore(rootPath, progress, cancellationToken),
            cancellationToken);
    }

    private static StorageScanResult ScanCore(
        string rootPath,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var rootInfo = new DirectoryInfo(fullRootPath);

        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"The selected directory does not exist: {fullRootPath}");
        }

        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The selected root is a reparse point. Select its target directory instead.");
        }

        var rootName = string.IsNullOrEmpty(rootInfo.Name) ? fullRootPath : rootInfo.Name;
        var root = StorageNode.CreateDirectory(
            rootName,
            fullRootPath,
            rootInfo.CreationTimeUtc,
            rootInfo.LastWriteTimeUtc);
        var directories = new Stack<StorageNode>();
        directories.Push(root);

        long filesScanned = 0;
        long directoriesScanned = 1;
        long bytesScanned = 0;
        var inaccessibleEntries = 0;
        var skippedReparsePoints = 0;
        var visitedEntries = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastProgressTime = stopwatch.Elapsed;

        ReportProgress(progress, root.FullPath, filesScanned, directoriesScanned, bytesScanned,
            inaccessibleEntries, skippedReparsePoints);

        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                foreach (var entryPath in Directory.EnumerateFileSystemEntries(directory.FullPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    visitedEntries++;

                    try
                    {
                        var attributes = File.GetAttributes(entryPath);

                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            skippedReparsePoints++;
                            continue;
                        }

                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            var directoryInfo = new DirectoryInfo(entryPath);
                            var childDirectory = StorageNode.CreateDirectory(
                                directoryInfo.Name,
                                directoryInfo.FullName,
                                directoryInfo.CreationTimeUtc,
                                directoryInfo.LastWriteTimeUtc);

                            directory.AddChild(childDirectory);
                            directories.Push(childDirectory);
                            directoriesScanned++;
                        }
                        else
                        {
                            var fileInfo = new FileInfo(entryPath);
                            var size = fileInfo.Length;
                            var file = StorageNode.CreateFile(
                                fileInfo.Name,
                                fileInfo.FullName,
                                size,
                                fileInfo.Extension,
                                fileInfo.CreationTimeUtc,
                                fileInfo.LastWriteTimeUtc);

                            directory.AddChild(file);
                            filesScanned++;
                            bytesScanned = checked(bytesScanned + size);
                        }
                    }
                    catch (Exception exception) when (IsExpectedFileSystemException(exception))
                    {
                        inaccessibleEntries++;
                    }

                    if (ShouldReportProgress(visitedEntries, stopwatch.Elapsed, lastProgressTime))
                    {
                        ReportProgress(progress, entryPath, filesScanned, directoriesScanned, bytesScanned,
                            inaccessibleEntries, skippedReparsePoints);
                        lastProgressTime = stopwatch.Elapsed;
                    }
                }
            }
            catch (Exception exception) when (IsExpectedFileSystemException(exception))
            {
                inaccessibleEntries++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, directory.FullPath, filesScanned, directoriesScanned, bytesScanned,
                inaccessibleEntries, skippedReparsePoints);
        }

        stopwatch.Stop();
        ReportProgress(progress, root.FullPath, filesScanned, directoriesScanned, bytesScanned,
            inaccessibleEntries, skippedReparsePoints);

        return new StorageScanResult(
            root,
            filesScanned,
            directoriesScanned,
            inaccessibleEntries,
            skippedReparsePoints,
            stopwatch.Elapsed);
    }

    private static bool TryGetReadyState(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (Exception exception) when (IsExpectedFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool ShouldReportProgress(
        int visitedEntries,
        TimeSpan elapsed,
        TimeSpan lastProgressTime) =>
        visitedEntries % ProgressEntryInterval == 0 ||
        elapsed - lastProgressTime >= ProgressTimeInterval;

    private static void ReportProgress(
        IProgress<StorageScanProgress>? progress,
        string currentPath,
        long filesScanned,
        long directoriesScanned,
        long bytesScanned,
        int inaccessibleEntries,
        int skippedReparsePoints)
    {
        progress?.Report(new StorageScanProgress(
            currentPath,
            filesScanned,
            directoriesScanned,
            bytesScanned,
            inaccessibleEntries,
            skippedReparsePoints));
    }

    private static bool IsExpectedFileSystemException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or SecurityException or
            ArgumentException or NotSupportedException;
}
