namespace Orbitera.Application;

public sealed record StorageScanProgress(
    string CurrentPath,
    long FilesScanned,
    long DirectoriesScanned,
    long BytesScanned,
    int InaccessibleEntries,
    int SkippedReparsePoints);
