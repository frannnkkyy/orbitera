namespace Orbitera.Domain;

public sealed record OrbitScoreInput(
    long? TotalCapacityBytes,
    long? AvailableBytes,
    double? MonthlyGrowthPercent,
    long? DuplicateBytes,
    long? RegenerableBytes,
    long? ChangedBytes);
