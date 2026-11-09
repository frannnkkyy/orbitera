namespace Orbitera.Domain;

public static class CleanupPlanSimulator
{
    public static CleanupPlanSimulation Simulate(
        CleanupPlan plan,
        long? totalCapacityBytes,
        long? availableBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (totalCapacityBytes is null && availableBytes is null)
        {
            return new CleanupPlanSimulation(plan, null, null, null);
        }

        if (totalCapacityBytes is not long capacity || availableBytes is not long available)
        {
            throw new ArgumentException("Total capacity and available space must either both be provided or both be unknown.");
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCapacityBytes), "Total capacity must be positive.");
        }

        if (available < 0 || available > capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(availableBytes), "Available bytes must be between zero and total capacity.");
        }

        var reclaimableHeadroom = capacity - available;
        var projectedAvailable = available + Math.Min(plan.EstimatedRecoverableBytes, reclaimableHeadroom);

        return new CleanupPlanSimulation(plan, available, projectedAvailable, capacity);
    }
}
