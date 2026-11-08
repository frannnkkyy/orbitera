namespace Orbitera.Domain;

public sealed record DevelopmentInsightRule(
    string DirectoryName,
    DevelopmentEcosystem Ecosystem,
    DevelopmentInsightCategory Category,
    string Purpose,
    string DeletionImpact,
    DevelopmentRiskLevel RiskLevel,
    bool IsPotentiallyRecoverable,
    IReadOnlyList<string>? AdditionalRequiredMarkers = null);
