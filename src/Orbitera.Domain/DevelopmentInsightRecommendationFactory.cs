namespace Orbitera.Domain;

public sealed class DevelopmentInsightRecommendationFactory
{
    public IReadOnlyList<DevelopmentInsightRecommendation> Create(
        IEnumerable<ClassifiedDevelopmentArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        return artifacts
            .Select(artifact => new DevelopmentInsightRecommendation(
                artifact.Project.Name,
                artifact.Project.RootPath,
                artifact.Node.Name,
                artifact.Node.FullPath,
                artifact.Node.SizeBytes,
                artifact.Rule.Ecosystem,
                artifact.Rule.Category,
                artifact.Rule.RiskLevel,
                artifact.Rule.IsPotentiallyRecoverable,
                artifact.Rule.Purpose,
                artifact.Rule.DeletionImpact))
            .ToArray();
    }
}
