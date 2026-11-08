namespace Orbitera.Domain;

public static class StorageCategoryClassifier
{
    private static readonly HashSet<string> DevelopmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".obj", ".lib", ".so", ".dylib", ".class", ".jar", ".wasm"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".heic", ".jpeg", ".jpg", ".png", ".psd", ".raw", ".svg", ".tif", ".tiff", ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".webm", ".wmv"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".aiff", ".flac", ".m4a", ".mp3", ".ogg", ".wav", ".wma"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".7z", ".bz2", ".cab", ".gz", ".rar", ".tar", ".zip"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".doc", ".docx", ".md", ".odt", ".pdf", ".ppt", ".pptx", ".rtf", ".txt", ".xls", ".xlsx"
    };

    public static StorageCategory Classify(StorageNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.IsDirectory)
        {
            return StorageCategory.Folder;
        }

        var extension = node.Extension;

        if (DevelopmentExtensions.Contains(extension))
        {
            return StorageCategory.Development;
        }

        if (ImageExtensions.Contains(extension))
        {
            return StorageCategory.Image;
        }

        if (VideoExtensions.Contains(extension))
        {
            return StorageCategory.Video;
        }

        if (AudioExtensions.Contains(extension))
        {
            return StorageCategory.Audio;
        }

        if (ArchiveExtensions.Contains(extension))
        {
            return StorageCategory.Archive;
        }

        if (DocumentExtensions.Contains(extension))
        {
            return StorageCategory.Document;
        }

        return extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msix", StringComparison.OrdinalIgnoreCase)
            ? StorageCategory.Application
            : StorageCategory.Other;
    }
}
