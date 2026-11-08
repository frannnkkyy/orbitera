namespace Orbitera.Domain;

public static class SquarifiedTreemapLayout
{
    public static IReadOnlyList<TreemapLayoutItem> Layout(
        IEnumerable<StorageNode> nodes,
        TreemapBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (bounds.Width < 0 || bounds.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Treemap dimensions cannot be negative.");
        }

        var items = nodes
            .Where(node => node.SizeBytes > 0)
            .OrderByDescending(node => node.SizeBytes)
            .ToList();

        if (items.Count == 0 || bounds.Area <= 0)
        {
            return [];
        }

        var totalSize = items.Sum(node => (double)node.SizeBytes);
        var remaining = items
            .Select(node => new WeightedNode(node, node.SizeBytes / totalSize * bounds.Area))
            .ToList();
        var result = new List<TreemapLayoutItem>(remaining.Count);
        var remainingBounds = bounds;

        while (remaining.Count > 0)
        {
            var row = new List<WeightedNode>();
            var shortSide = Math.Min(remainingBounds.Width, remainingBounds.Height);
            row.Add(remaining[0]);
            remaining.RemoveAt(0);

            while (remaining.Count > 0 &&
                   WorstAspectRatio(row, shortSide) >=
                   WorstAspectRatioWith(row, remaining[0], shortSide))
            {
                row.Add(remaining[0]);
                remaining.RemoveAt(0);
            }

            LayoutRow(row, ref remainingBounds, result);
        }

        return result;
    }

    private static double WorstAspectRatio(IReadOnlyCollection<WeightedNode> row, double shortSide)
    {
        var sum = row.Sum(item => item.Area);
        var minimum = row.Min(item => item.Area);
        var maximum = row.Max(item => item.Area);

        if (sum <= 0 || minimum <= 0 || shortSide <= 0)
        {
            return double.PositiveInfinity;
        }

        var sideSquared = shortSide * shortSide;
        return Math.Max(
            sideSquared * maximum / (sum * sum),
            sum * sum / (sideSquared * minimum));
    }

    private static double WorstAspectRatioWith(
        IReadOnlyCollection<WeightedNode> row,
        WeightedNode next,
        double shortSide)
    {
        var combined = new List<WeightedNode>(row) { next };
        return WorstAspectRatio(combined, shortSide);
    }

    private static void LayoutRow(
        IReadOnlyList<WeightedNode> row,
        ref TreemapBounds bounds,
        ICollection<TreemapLayoutItem> result)
    {
        var rowArea = row.Sum(item => item.Area);

        if (bounds.Width >= bounds.Height)
        {
            var stripWidth = Math.Min(bounds.Width, rowArea / bounds.Height);
            var y = bounds.Y;

            foreach (var item in row)
            {
                var height = stripWidth > 0 ? item.Area / stripWidth : 0;
                result.Add(new TreemapLayoutItem(
                    item.Node,
                    new TreemapBounds(bounds.X, y, stripWidth, Math.Min(height, bounds.Y + bounds.Height - y))));
                y += height;
            }

            bounds = new TreemapBounds(
                bounds.X + stripWidth,
                bounds.Y,
                Math.Max(0, bounds.Width - stripWidth),
                bounds.Height);
        }
        else
        {
            var stripHeight = Math.Min(bounds.Height, rowArea / bounds.Width);
            var x = bounds.X;

            foreach (var item in row)
            {
                var width = stripHeight > 0 ? item.Area / stripHeight : 0;
                result.Add(new TreemapLayoutItem(
                    item.Node,
                    new TreemapBounds(x, bounds.Y, Math.Min(width, bounds.X + bounds.Width - x), stripHeight)));
                x += width;
            }

            bounds = new TreemapBounds(
                bounds.X,
                bounds.Y + stripHeight,
                bounds.Width,
                Math.Max(0, bounds.Height - stripHeight));
        }
    }

    private sealed record WeightedNode(StorageNode Node, double Area);
}
