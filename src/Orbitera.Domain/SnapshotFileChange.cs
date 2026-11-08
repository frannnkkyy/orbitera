namespace Orbitera.Domain;

public enum SnapshotFileChangeKind
{
    Added,
    Removed,
    SizeChanged
}

public sealed record SnapshotFileChange(
    string RelativePath,
    SnapshotFileChangeKind Kind,
    long? PreviousSizeBytes,
    long? CurrentSizeBytes)
{
    public long SizeDeltaBytes => (CurrentSizeBytes ?? 0) - (PreviousSizeBytes ?? 0);
}
