namespace Orbitera.Domain;

public sealed record ClassifiedDevelopmentArtifact(
    StorageNode Node,
    DetectedDevelopmentProject Project,
    DevelopmentInsightRule Rule);
