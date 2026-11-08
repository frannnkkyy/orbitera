namespace Orbitera.Domain;

public sealed record StorageSnapshotSummary(
    long Id,
    string RootPath,
    DateTime CapturedUtc,
    long TotalSizeBytes,
    long FileCount,
    bool IsComplete);
