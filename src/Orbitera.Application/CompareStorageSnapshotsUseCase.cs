using Orbitera.Domain;

namespace Orbitera.Application;

public sealed class CompareStorageSnapshotsUseCase(ISnapshotRepository repository)
{
    public async Task<SnapshotComparison> ExecuteAsync(
        long previousSnapshotId,
        long currentSnapshotId,
        CancellationToken cancellationToken)
    {
        if (previousSnapshotId == currentSnapshotId)
        {
            throw new ArgumentException("Choose two different snapshots.", nameof(currentSnapshotId));
        }

        return await repository.CompareAsync(
            previousSnapshotId,
            currentSnapshotId,
            cancellationToken);
    }
}
