namespace Orbitera.Domain;

public sealed record CleanupPlanItem(
    string ProjectName,
    string DirectoryName,
    string Path,
    long SizeBytes,
    DevelopmentEcosystem Ecosystem,
    DevelopmentInsightCategory Category,
    DevelopmentRiskLevel RiskLevel,
    string Reason,
    string Impact);
