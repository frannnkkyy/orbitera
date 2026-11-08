using Orbitera.Domain;

namespace Orbitera.Application;

public interface ISnapshotRepository
{
    Task<StorageSnapshotSummary> SaveAsync(
        StorageNode root,
        DateTime capturedUtc,
        bool isComplete,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SnapshotRoot>> GetRootsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StorageSnapshotSummary>> GetSnapshotsAsync(
        string rootPath,
        CancellationToken cancellationToken);

    Task<StorageSnapshot> GetSnapshotAsync(long snapshotId, CancellationToken cancellationToken);

    Task<SnapshotComparison> CompareAsync(
        long previousSnapshotId,
        long currentSnapshotId,
        CancellationToken cancellationToken);
}
