namespace Orbitera.Domain;

public sealed class StorageNode
{
    private readonly List<StorageNode> _children = [];

    private StorageNode(
        string name,
        string fullPath,
        StorageNodeKind kind,
        long sizeBytes,
        string extension,
        DateTime? createdUtc,
        DateTime? lastModifiedUtc)
    {
        Name = name;
        FullPath = fullPath;
        Kind = kind;
        SizeBytes = sizeBytes;
        Extension = extension;
        CreatedUtc = createdUtc;
        LastModifiedUtc = lastModifiedUtc;
    }

    public string Name { get; }

    public string FullPath { get; }

    public StorageNodeKind Kind { get; }

    public long SizeBytes { get; private set; }

    public string Extension { get; }

    public DateTime? CreatedUtc { get; }

    public DateTime? LastModifiedUtc { get; }

    public StorageNode? Parent { get; private set; }

    public IReadOnlyList<StorageNode> Children => _children;

    public bool IsDirectory => Kind == StorageNodeKind.Directory;

    public static StorageNode CreateDirectory(
        string name,
        string fullPath,
        DateTime? createdUtc = null,
        DateTime? lastModifiedUtc = null) =>
        new(name, fullPath, StorageNodeKind.Directory, 0, string.Empty, createdUtc, lastModifiedUtc);

    public static StorageNode CreateFile(
        string name,
        string fullPath,
        long sizeBytes,
        string extension,
        DateTime? createdUtc = null,
        DateTime? lastModifiedUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        return new(name, fullPath, StorageNodeKind.File, sizeBytes, extension, createdUtc, lastModifiedUtc);
    }

    public void AddChild(StorageNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!IsDirectory)
        {
            throw new InvalidOperationException("Files cannot contain child entries.");
        }

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("A storage entry cannot belong to more than one directory.");
        }

        child.Parent = this;
        _children.Add(child);
        AddSize(child.SizeBytes);
    }

    private void AddSize(long sizeBytes)
    {
        SizeBytes = checked(SizeBytes + sizeBytes);
        Parent?.AddSize(sizeBytes);
    }
}
