namespace Orbitera.Domain;

public static class StorageSnapshotComparer
{
    public static SnapshotComparison Compare(StorageSnapshot previous, StorageSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var previousFiles = CreateFileMap(previous.Files);
        var currentFiles = CreateFileMap(current.Files);

        return CompareSorted(
            previous.Summary,
            current.Summary,
            previousFiles.Values.OrderBy(file => NormalizePathKey(file.RelativePath), StringComparer.Ordinal),
            currentFiles.Values.OrderBy(file => NormalizePathKey(file.RelativePath), StringComparer.Ordinal));
    }

    public static SnapshotComparison CompareSorted(
        StorageSnapshotSummary previous,
        StorageSnapshotSummary current,
        IEnumerable<SnapshotFileEntry> previousFiles,
        IEnumerable<SnapshotFileEntry> currentFiles)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previousFiles);
        ArgumentNullException.ThrowIfNull(currentFiles);

        if (!string.Equals(
                NormalizeRoot(previous.RootPath),
                NormalizeRoot(current.RootPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Snapshots must belong to the same storage root.", nameof(current));
        }

        if (previous.CapturedUtc > current.CapturedUtc)
        {
            throw new ArgumentException("The previous snapshot must not be newer than the current snapshot.", nameof(previous));
        }

        var added = new List<SnapshotFileChange>();
        var removed = new List<SnapshotFileChange>();
        var resized = new List<SnapshotFileChange>();
        var folderDeltas = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var previousEnumerator = previousFiles.GetEnumerator();
        using var currentEnumerator = currentFiles.GetEnumerator();
        var hasPrevious = previousEnumerator.MoveNext();
        var hasCurrent = currentEnumerator.MoveNext();
        string? previousKey = null;
        string? currentKey = null;

        while (hasPrevious || hasCurrent)
        {
            var previousFile = hasPrevious ? previousEnumerator.Current : null;
            var currentFile = hasCurrent ? currentEnumerator.Current : null;
            previousKey = previousFile is null ? null : NormalizePathKey(previousFile.RelativePath);
            currentKey = currentFile is null ? null : NormalizePathKey(currentFile.RelativePath);

            if (previousFile is not null &&
                currentFile is not null &&
                StringComparer.Ordinal.Equals(previousKey, currentKey))
            {
                if (previousFile.SizeBytes != currentFile.SizeBytes)
                {
                    var resizedChange = new SnapshotFileChange(
                        currentFile.RelativePath,
                        SnapshotFileChangeKind.SizeChanged,
                        previousFile.SizeBytes,
                        currentFile.SizeBytes);
                    resized.Add(resizedChange);
                    AddFolderDelta(folderDeltas, currentFile.RelativePath, resizedChange.SizeDeltaBytes);
                }

                hasPrevious = previousEnumerator.MoveNext();
                hasCurrent = currentEnumerator.MoveNext();
                continue;
            }

            if (currentFile is not null &&
                (previousFile is null || StringComparer.Ordinal.Compare(currentKey, previousKey) < 0))
            {
                var addedChange = new SnapshotFileChange(
                    currentFile.RelativePath,
                    SnapshotFileChangeKind.Added,
                    null,
                    currentFile.SizeBytes);
                added.Add(addedChange);
                AddFolderDelta(folderDeltas, currentFile.RelativePath, addedChange.SizeDeltaBytes);
                hasCurrent = currentEnumerator.MoveNext();
                continue;
            }

            var removedPath = previousFile!.RelativePath.Replace('/', '\\').TrimStart('\\');
            var removedChange = new SnapshotFileChange(removedPath, SnapshotFileChangeKind.Removed, previousFile.SizeBytes, null);
            removed.Add(removedChange);
            AddFolderDelta(folderDeltas, removedPath, removedChange.SizeDeltaBytes);
            hasPrevious = previousEnumerator.MoveNext();
        }

        var folderGrowths = folderDeltas
            .Select(pair => new FolderGrowth(pair.Key, pair.Value))
            .OrderByDescending(folder => folder.NetGrowthBytes)
            .ThenBy(folder => folder.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SnapshotComparison(
            previous,
            current,
            added,
            removed,
            resized,
            folderGrowths);
    }

    private static Dictionary<string, SnapshotFileEntry> CreateFileMap(
        IEnumerable<SnapshotFileEntry> files)
    {
        var result = new Dictionary<string, SnapshotFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relativePath = NormalizeRelativePath(file.RelativePath);

            if (!result.TryAdd(NormalizePathKey(relativePath), file with { RelativePath = relativePath }))
            {
                throw new ArgumentException(
                    $"Snapshot contains duplicate file paths that differ only by case: {relativePath}",
                    nameof(files));
            }
        }

        return result;
    }

    private static void AddFolderDelta(
        IDictionary<string, long> folderDeltas,
        string relativeFilePath,
        long delta)
    {
        var separator = relativeFilePath.LastIndexOf('\\');
        var folder = separator < 0 ? string.Empty : relativeFilePath[..separator];

        while (true)
        {
            folderDeltas.TryGetValue(folder, out var currentDelta);
            folderDeltas[folder] = checked(currentDelta + delta);

            if (folder.Length == 0)
            {
                break;
            }

            separator = folder.LastIndexOf('\\');
            folder = separator < 0 ? string.Empty : folder[..separator];
        }
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('/', '\\').TrimStart('\\');

    private static string NormalizePathKey(string path) =>
        NormalizeRelativePath(path).ToUpperInvariant();

    private static string NormalizeRoot(string path) =>
        path.Replace('/', '\\').TrimEnd('\\');
}
