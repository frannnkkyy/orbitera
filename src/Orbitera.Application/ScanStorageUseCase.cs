namespace Orbitera.Application;

public sealed class ScanStorageUseCase(IStorageScanner scanner)
{
    public IReadOnlyList<StorageVolume> GetAvailableVolumes() =>
        scanner.GetAvailableVolumes();

    public Task<StorageScanResult> ExecuteAsync(
        string rootPath,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();

        return scanner.ScanAsync(rootPath, progress, cancellationToken);
    }
}
