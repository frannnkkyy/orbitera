using Orbitera.Infrastructure;

namespace Orbitera.Infrastructure.Tests;

public sealed class DriveStorageCapacityProviderTests
{
    [Fact]
    public async Task ReadsCapacityForVolumeContainingPath()
    {
        var provider = new DriveStorageCapacityProvider();

        var capacity = await provider.GetCapacityAsync(Path.GetTempPath(), CancellationToken.None);

        Assert.NotNull(capacity);
        Assert.True(capacity.TotalBytes > 0);
        Assert.InRange(capacity.AvailableBytes, 0, capacity.TotalBytes);
    }

    [Fact]
    public async Task HonorsCancellationBeforeReadingDriveInformation()
    {
        var provider = new DriveStorageCapacityProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetCapacityAsync(Path.GetTempPath(), cancellation.Token));
    }
}
