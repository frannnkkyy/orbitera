using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void LayoutPreservesSizeProportionsAndCoversAvailableArea()
    {
        var root = StorageNode.CreateDirectory("root", @"C:\root");
        var large = StorageNode.CreateFile("large.bin", @"C:\root\large.bin", 50, ".bin");
        var medium = StorageNode.CreateFile("medium.bin", @"C:\root\medium.bin", 30, ".bin");
        var small = StorageNode.CreateFile("small.bin", @"C:\root\small.bin", 20, ".bin");
        root.AddChild(large);
        root.AddChild(medium);
        root.AddChild(small);

        var bounds = new TreemapBounds(0, 0, 100, 80);
        var layout = SquarifiedTreemapLayout.Layout(root.Children, bounds);

        Assert.Equal(3, layout.Count);
        Assert.Equal(bounds.Area, layout.Sum(item => item.Bounds.Area), precision: 6);
        Assert.Equal(
            2.5,
            layout.Single(item => item.Node == large).Bounds.Area /
            layout.Single(item => item.Node == small).Bounds.Area,
            precision: 6);
        Assert.All(layout, item =>
        {
            Assert.InRange(item.Bounds.X, bounds.X, bounds.X + bounds.Width);
            Assert.InRange(item.Bounds.Y, bounds.Y, bounds.Y + bounds.Height);
            Assert.True(item.Bounds.X + item.Bounds.Width <= bounds.X + bounds.Width + 1e-6);
            Assert.True(item.Bounds.Y + item.Bounds.Height <= bounds.Y + bounds.Height + 1e-6);
            Assert.True(item.Bounds.Width > 0);
            Assert.True(item.Bounds.Height > 0);
        });

        for (var first = 0; first < layout.Count; first++)
        {
            for (var second = first + 1; second < layout.Count; second++)
            {
                var firstBounds = layout[first].Bounds;
                var secondBounds = layout[second].Bounds;
                var overlapWidth = Math.Min(
                    firstBounds.X + firstBounds.Width,
                    secondBounds.X + secondBounds.Width) -
                    Math.Max(firstBounds.X, secondBounds.X);
                var overlapHeight = Math.Min(
                    firstBounds.Y + firstBounds.Height,
                    secondBounds.Y + secondBounds.Height) -
                    Math.Max(firstBounds.Y, secondBounds.Y);

                Assert.False(overlapWidth > 1e-6 && overlapHeight > 1e-6);
            }
        }
    }

    [Fact]
    public void LayoutOmitsZeroLengthItemsAndReturnsEmptyForEmptyBounds()
    {
        var emptyFile = StorageNode.CreateFile("empty.txt", @"C:\root\empty.txt", 0, ".txt");

        Assert.Empty(SquarifiedTreemapLayout.Layout(
            [emptyFile],
            new TreemapBounds(0, 0, 100, 80)));
        Assert.Empty(SquarifiedTreemapLayout.Layout(
            [StorageNode.CreateFile("data.bin", @"C:\data.bin", 10, ".bin")],
            new TreemapBounds(0, 0, 0, 80)));
    }

    [Fact]
    public void LayoutRejectsNegativeBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SquarifiedTreemapLayout.Layout(
                [],
                new TreemapBounds(0, 0, -1, 80)));
    }
}
