namespace Orbitera.Domain;

public sealed record CleanupPlanSimulation(
    CleanupPlan Plan,
    long? AvailableBeforeBytes,
    long? ProjectedAvailableBytes,
    long? TotalCapacityBytes)
{
    public long EstimatedRecoverableBytes => Plan.EstimatedRecoverableBytes;
}
