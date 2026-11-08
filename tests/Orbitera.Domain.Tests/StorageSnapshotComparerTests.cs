using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public class StorageSnapshotComparerTests
{
    [Fact]
    public void CompareIdentifiesAddedRemovedAndResizedFilesAndAggregatesFolderGrowth()
    {
        var previous = CreateSnapshot(
            id: 1,
            @"C:\Projects",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            totalSizeBytes: 60,
            (@"src\keep.cs", 10L),
            (@"src\change.dll", 20L),
            (@"old\removed.txt", 30L));
        var current = CreateSnapshot(
            id: 2,
            @"c:\projects\",
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            totalSizeBytes: 75,
            (@"SRC\KEEP.CS", 10L),
            (@"src\change.dll", 25L),
            (@"src\new.txt", 40L));

        var comparison = StorageSnapshotComparer.Compare(previous, current);

        Assert.Single(comparison.AddedFiles);
        Assert.Equal(@"src\new.txt", comparison.AddedFiles[0].RelativePath);
        Assert.Single(comparison.RemovedFiles);
        Assert.Equal(@"old\removed.txt", comparison.RemovedFiles[0].RelativePath);
        Assert.Single(comparison.ResizedFiles);
        Assert.Equal(5, comparison.ResizedFiles[0].SizeDeltaBytes);
        Assert.Equal(15, comparison.NetGrowthBytes);
        Assert.Equal(45, comparison.FolderGrowths.Single(folder => folder.RelativePath == "src").NetGrowthBytes);
        Assert.Equal(15, comparison.FolderGrowths.Single(folder => folder.RelativePath == string.Empty).NetGrowthBytes);
    }

    [Fact]
    public void CompareTreatsMovedOrRenamedPathAsAddedAndRemovedInsteadOfAssumingIdentity()
    {
        var previous = CreateSnapshot(
            1,
            @"C:\Projects",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            100,
            (@"old\report.pdf", 100L));
        var current = CreateSnapshot(
            2,
            @"C:\Projects",
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            100,
            (@"archive\report.pdf", 100L));

        var comparison = StorageSnapshotComparer.Compare(previous, current);

        Assert.Single(comparison.AddedFiles);
        Assert.Single(comparison.RemovedFiles);
        Assert.Empty(comparison.ResizedFiles);
    }

    [Fact]
    public void CompareSortedProducesSameDiffWithoutMaterializingEntryDictionaries()
    {
        var previousSummary = new StorageSnapshotSummary(
            1,
            @"C:\Projects",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            12,
            2,
            true);
        var currentSummary = previousSummary with
        {
            Id = 2,
            CapturedUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            TotalSizeBytes = 17
        };
        var previousFiles = new[]
        {
            new SnapshotFileEntry(@"a\keep.txt", 5, null),
            new SnapshotFileEntry(@"b\old.txt", 7, null)
        };
        var currentFiles = new[]
        {
            new SnapshotFileEntry(@"a\keep.txt", 5, null),
            new SnapshotFileEntry(@"b\new.txt", 12, null)
        };

        var comparison = StorageSnapshotComparer.CompareSorted(
            previousSummary,
            currentSummary,
            previousFiles,
            currentFiles);

        Assert.Single(comparison.AddedFiles);
        Assert.Single(comparison.RemovedFiles);
        Assert.Equal(5, comparison.NetGrowthBytes);
    }

    [Fact]
    public void CompareRejectsDifferentRootsAndSnapshotsInReverseChronologicalOrder()
    {
        var earlier = CreateSnapshot(
            1,
            @"C:\Projects",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            0);
        var later = CreateSnapshot(
            2,
            @"D:\Projects",
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            0);

        Assert.Throws<ArgumentException>(() => StorageSnapshotComparer.Compare(earlier, later));
        Assert.Throws<ArgumentException>(() => StorageSnapshotComparer.Compare(later, earlier with
        {
            Summary = earlier.Summary with { RootPath = later.Summary.RootPath }
        }));
    }

    private static StorageSnapshot CreateSnapshot(
        long id,
        string rootPath,
        DateTime capturedUtc,
        long totalSizeBytes,
        params (string Path, long Size)[] files) =>
        new(
            new StorageSnapshotSummary(id, rootPath, capturedUtc, totalSizeBytes, files.Length, true),
            files.Select(file => new SnapshotFileEntry(file.Path, file.Size, capturedUtc)).ToArray());
}
