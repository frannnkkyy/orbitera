namespace Orbitera.Domain;

public sealed record StorageSnapshot(
    StorageSnapshotSummary Summary,
    IReadOnlyList<SnapshotFileEntry> Files);
