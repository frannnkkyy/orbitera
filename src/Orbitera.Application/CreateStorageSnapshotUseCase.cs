using Orbitera.Domain;

namespace Orbitera.Application;

public sealed class CreateStorageSnapshotUseCase(ISnapshotRepository repository)
{
    public Task<StorageSnapshotSummary> ExecuteAsync(
        StorageScanResult scanResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        cancellationToken.ThrowIfCancellationRequested();

        return repository.SaveAsync(
            scanResult.Root,
            DateTime.UtcNow,
            scanResult.InaccessibleEntries == 0 && scanResult.SkippedReparsePoints == 0,
            cancellationToken);
    }
}
