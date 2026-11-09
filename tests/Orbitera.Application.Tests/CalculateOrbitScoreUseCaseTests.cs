using Orbitera.Application;
using Orbitera.Domain;

namespace Orbitera.Application.Tests;

public sealed class CalculateOrbitScoreUseCaseTests
{
    [Fact]
    public async Task CombinesCapacityAndCompleteSnapshotEvidence()
    {
        var captured = DateTime.UtcNow;
        var previous = new StorageSnapshotSummary(1, @"C:\scan", captured.AddDays(-30), 200, 1, true);
        var current = new StorageSnapshotSummary(2, @"C:\scan", captured, 300, 2, true);
        var comparison = new SnapshotComparison(
            previous,
            current,
            [new SnapshotFileChange(@"added.bin", SnapshotFileChangeKind.Added, null, 100)],
            [],
            [],
            []);
        var repository = new SnapshotRepositoryStub([current, previous], comparison);
        var session = new StorageAnalysisSession();
        session.Publish(StorageNode.CreateDirectory("scan", @"C:\scan"), captured);
        var rules = new DevelopmentInsightsRules();
        var analyzeInsights = new AnalyzeDevelopmentInsightsUseCase(
            session,
            new DevelopmentInsightsAnalyzer(
                new DevelopmentProjectDetector(rules),
                new DevelopmentArtifactClassifier(rules),
                new DevelopmentInsightRecommendationFactory()));
        var useCase = new CalculateOrbitScoreUseCase(
            session,
            new CapacityProviderStub(new StorageCapacity(1_000, 250)),
            repository,
            analyzeInsights);

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(85, result.CoveragePercent);
        Assert.Equal(0, Assert.Single(result.Factors, factor => factor.Name == "Growth trend").Score);
        Assert.Equal(53, Assert.Single(result.Factors, factor => factor.Name == "Changes between scans").Score);
        Assert.DoesNotContain(result.Factors, factor => factor.Name == "Identified duplicates");
    }

    private sealed class CapacityProviderStub(StorageCapacity? capacity) : IStorageCapacityProvider
    {
        public Task<StorageCapacity?> GetCapacityAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(capacity);
    }

    private sealed class SnapshotRepositoryStub(
        IReadOnlyList<StorageSnapshotSummary> snapshots,
        SnapshotComparison comparison) : ISnapshotRepository
    {
        public Task<StorageSnapshotSummary> SaveAsync(
            StorageNode root,
            DateTime capturedUtc,
            bool isComplete,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotRoot>> GetRootsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SnapshotRoot>>([]);

        public Task<IReadOnlyList<StorageSnapshotSummary>> GetSnapshotsAsync(
            string rootPath,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshots);

        public Task<StorageSnapshot> GetSnapshotAsync(long snapshotId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SnapshotComparison> CompareAsync(
            long previousSnapshotId,
            long currentSnapshotId,
            CancellationToken cancellationToken) =>
            Task.FromResult(comparison);
    }
}
