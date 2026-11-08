namespace Orbitera.Domain;

public sealed record DevelopmentInsightsResult(
    IReadOnlyList<DetectedDevelopmentProject> Projects,
    IReadOnlyList<DevelopmentInsightRecommendation> Recommendations)
{
    public long PotentiallyRecoverableBytes =>
        Recommendations
            .Where(recommendation => recommendation.IsPotentiallyRecoverable)
            .Sum(recommendation => recommendation.SizeBytes);
}

public sealed class DevelopmentInsightsAnalyzer(
    DevelopmentProjectDetector projectDetector,
    DevelopmentArtifactClassifier artifactClassifier,
    DevelopmentInsightRecommendationFactory recommendationFactory)
{
    public DevelopmentInsightsResult Analyze(
        StorageNode scanRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);
        cancellationToken.ThrowIfCancellationRequested();
        var projects = projectDetector.Detect(scanRoot, cancellationToken);
        var artifacts = artifactClassifier.Classify(scanRoot, projects, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var recommendations = recommendationFactory.Create(artifacts);
        return new DevelopmentInsightsResult(projects, recommendations);
    }
}
