namespace Orbitera.Domain;

public sealed record CleanupPlan(
    DateTime CreatedUtc,
    IReadOnlyList<CleanupPlanItem> Items,
    long EstimatedRecoverableBytes)
{
    public bool IsReadOnly => true;
}
