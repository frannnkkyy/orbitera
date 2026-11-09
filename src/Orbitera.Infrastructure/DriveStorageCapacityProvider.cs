using Orbitera.Application;
using System.IO;

namespace Orbitera.Infrastructure;

public sealed class DriveStorageCapacityProvider : IStorageCapacityProvider
{
    public Task<StorageCapacity?> GetCapacityAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var driveRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(driveRoot))
        {
            return Task.FromResult<StorageCapacity?>(null);
        }

        var drive = DriveInfo.GetDrives().FirstOrDefault(candidate =>
            string.Equals(candidate.RootDirectory.FullName, driveRoot, StringComparison.OrdinalIgnoreCase));

        if (drive is null || !drive.IsReady)
        {
            return Task.FromResult<StorageCapacity?>(null);
        }

        var capacity = new StorageCapacity(drive.TotalSize, drive.AvailableFreeSpace);
        return Task.FromResult<StorageCapacity?>(capacity);
    }
}
