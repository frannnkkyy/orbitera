namespace Orbitera.Domain;

public sealed record OrbitScoreResult(
    int Score,
    int CoveragePercent,
    IReadOnlyList<OrbitScoreFactor> Factors);
