namespace Orbitera.Domain;

public sealed record SnapshotComparison(
    StorageSnapshotSummary Previous,
    StorageSnapshotSummary Current,
    IReadOnlyList<SnapshotFileChange> AddedFiles,
    IReadOnlyList<SnapshotFileChange> RemovedFiles,
    IReadOnlyList<SnapshotFileChange> ResizedFiles,
    IReadOnlyList<FolderGrowth> FolderGrowths)
{
    public long NetGrowthBytes => Current.TotalSizeBytes - Previous.TotalSizeBytes;
}
