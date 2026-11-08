using Orbitera.Domain;

namespace Orbitera.Application;

public sealed class StorageAnalysisSession
{
    private readonly object _sync = new();
    private StorageNode? _latestRoot;
    private DateTime? _capturedUtc;

    public StorageNode? LatestRoot
    {
        get
        {
            lock (_sync)
            {
                return _latestRoot;
            }
        }
    }

    public DateTime? CapturedUtc
    {
        get
        {
            lock (_sync)
            {
                return _capturedUtc;
            }
        }
    }

    public void Publish(StorageNode root, DateTime capturedUtc)
    {
        ArgumentNullException.ThrowIfNull(root);

        lock (_sync)
        {
            _latestRoot = root;
            _capturedUtc = capturedUtc.ToUniversalTime();
        }
    }
}
