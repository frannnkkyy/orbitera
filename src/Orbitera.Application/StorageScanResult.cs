using Orbitera.Domain;

namespace Orbitera.Application;

public sealed record StorageScanResult(
    StorageNode Root,
    long FilesScanned,
    long DirectoriesScanned,
    int InaccessibleEntries,
    int SkippedReparsePoints,
    TimeSpan Duration);
