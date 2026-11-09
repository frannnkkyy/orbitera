using Orbitera.Domain;

namespace Orbitera.Application;

public sealed class CalculateOrbitScoreUseCase(
    StorageAnalysisSession session,
    IStorageCapacityProvider capacityProvider,
    ISnapshotRepository snapshotRepository,
    AnalyzeDevelopmentInsightsUseCase analyzeDevelopmentInsights)
{
    public async Task<OrbitScoreResult?> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = session.LatestRoot;

        if (root is null)
        {
            return null;
        }

        var capacity = await capacityProvider.GetCapacityAsync(root.FullPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        double? monthlyGrowthPercent = null;
        long? changedBytes = null;

        var snapshots = await snapshotRepository.GetSnapshotsAsync(root.FullPath, cancellationToken);
        if (snapshots.Count >= 2)
        {
            var current = snapshots[0];
            var previous = snapshots[1];
            var elapsedDays = (current.CapturedUtc - previous.CapturedUtc).TotalDays;

            if (current.IsComplete &&
                previous.IsComplete &&
                elapsedDays > 0 &&
                capacity is { TotalBytes: > 0 })
            {
                var netGrowth = current.TotalSizeBytes - previous.TotalSizeBytes;
                monthlyGrowthPercent = netGrowth / (double)capacity.TotalBytes * 100 * 30 / elapsedDays;

                var comparison = await snapshotRepository.CompareAsync(
                    previous.Id,
                    current.Id,
                    cancellationToken);
                changedBytes = CalculateChangedBytes(comparison);
            }
        }

        var insights = await Task.Run(
            () => analyzeDevelopmentInsights.Execute(cancellationToken),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return OrbitScoreCalculator.Calculate(new OrbitScoreInput(
            capacity?.TotalBytes,
            capacity?.AvailableBytes,
            monthlyGrowthPercent,
            DuplicateBytes: null,
            insights?.PotentiallyRecoverableBytes,
            changedBytes));
    }

    private static long CalculateChangedBytes(SnapshotComparison comparison)
    {
        var changedBytes = 0L;

        foreach (var change in comparison.AddedFiles)
        {
            changedBytes = checked(changedBytes + change.CurrentSizeBytes!.Value);
        }

        foreach (var change in comparison.RemovedFiles)
        {
            changedBytes = checked(changedBytes + change.PreviousSizeBytes!.Value);
        }

        foreach (var change in comparison.ResizedFiles)
        {
            changedBytes = checked(changedBytes + Math.Abs(change.SizeDeltaBytes));
        }

        return changedBytes;
    }
}
