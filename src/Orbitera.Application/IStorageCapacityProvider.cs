namespace Orbitera.Application;

public interface IStorageCapacityProvider
{
    Task<StorageCapacity?> GetCapacityAsync(string path, CancellationToken cancellationToken);
}
