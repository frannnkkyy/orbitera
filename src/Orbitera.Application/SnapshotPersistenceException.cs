namespace Orbitera.Application;

public sealed class SnapshotPersistenceException(string message, Exception innerException)
    : Exception(message, innerException);
