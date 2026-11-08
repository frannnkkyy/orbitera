namespace Orbitera.Domain;

public sealed record SnapshotFileEntry(
    string RelativePath,
    long SizeBytes,
    DateTime? LastModifiedUtc);
