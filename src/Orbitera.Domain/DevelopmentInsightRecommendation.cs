namespace Orbitera.Domain;

public sealed record DevelopmentInsightRecommendation(
    string ProjectName,
    string ProjectPath,
    string DirectoryName,
    string DirectoryPath,
    long SizeBytes,
    DevelopmentEcosystem Ecosystem,
    DevelopmentInsightCategory Category,
    DevelopmentRiskLevel RiskLevel,
    bool IsPotentiallyRecoverable,
    string Purpose,
    string DeletionImpact);
