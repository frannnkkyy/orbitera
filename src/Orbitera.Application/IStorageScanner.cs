using Orbitera.Domain;

namespace Orbitera.Application;

public interface IStorageScanner
{
    IReadOnlyList<StorageVolume> GetAvailableVolumes();

    Task<StorageScanResult> ScanAsync(
        string rootPath,
        IProgress<StorageScanProgress>? progress,
        CancellationToken cancellationToken);
}
