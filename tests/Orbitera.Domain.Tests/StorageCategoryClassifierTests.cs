using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public class StorageCategoryClassifierTests
{
    [Theory]
    [InlineData(".PNG", StorageCategory.Image)]
    [InlineData(".mp4", StorageCategory.Video)]
    [InlineData(".zip", StorageCategory.Archive)]
    [InlineData(".dll", StorageCategory.Development)]
    [InlineData(".pdf", StorageCategory.Document)]
    [InlineData(".unknown", StorageCategory.Other)]
    public void ClassifiesFileByExtension(string extension, StorageCategory expected)
    {
        var file = StorageNode.CreateFile("item" + extension, @"C:\item" + extension, 1, extension);

        Assert.Equal(expected, StorageCategoryClassifier.Classify(file));
    }

    [Fact]
    public void ClassifiesDirectoriesAsFolders()
    {
        var directory = StorageNode.CreateDirectory("folder", @"C:\folder");

        Assert.Equal(StorageCategory.Folder, StorageCategoryClassifier.Classify(directory));
    }
}
