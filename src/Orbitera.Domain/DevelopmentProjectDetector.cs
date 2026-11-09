namespace Orbitera.Domain;

public sealed class DevelopmentProjectDetector(DevelopmentInsightsRules rules)
{
    public IReadOnlyList<DetectedDevelopmentProject> Detect(
        StorageNode scanRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);

        var matches = new Dictionary<StorageNode, ProjectMatch>();
        var directories = EnumerateDirectories(scanRoot);

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsInsideArtifactDirectory(directory))
            {
                continue;
            }

            foreach (var indicator in rules.ProjectIndicators)
            {
                var match = directory.Children.FirstOrDefault(child =>
                    child.IsDirectory == indicator.IsDirectory &&
                    Matches(child.Name, indicator.FileName));

                if (match is null)
                {
                    continue;
                }

                if (!matches.TryGetValue(directory, out var projectMatch))
                {
                    projectMatch = new ProjectMatch();
                    matches.Add(directory, projectMatch);
                }

                projectMatch.Ecosystems.Add(indicator.Ecosystem);
                projectMatch.Indicators.Add(match.Name);
            }
        }

        return matches
            .Select(pair => new DetectedDevelopmentProject(
                pair.Key.Name,
                pair.Key.FullPath,
                pair.Value.Ecosystems,
                pair.Value.Indicators.Order(StringComparer.OrdinalIgnoreCase).ToArray()))
            .OrderBy(project => project.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<StorageNode> EnumerateDirectories(StorageNode root)
    {
        var stack = new Stack<StorageNode>();
        stack.Push(root);

        while (stack.TryPop(out var directory))
        {
            if (!directory.IsDirectory)
            {
                continue;
            }

            yield return directory;

            foreach (var child in directory.Children)
            {
                if (child.IsDirectory)
                {
                    stack.Push(child);
                }
            }
        }
    }

    private static bool Matches(string name, string pattern) =>
        pattern.StartsWith("*.", StringComparison.Ordinal)
            ? name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : name.Equals(pattern, StringComparison.OrdinalIgnoreCase);

    private bool IsInsideArtifactDirectory(StorageNode directory)
    {
        for (StorageNode? current = directory; current is not null; current = current.Parent)
        {
            if (rules.ArtifactRules.Any(rule =>
                    current.Name.Equals(rule.DirectoryName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ProjectMatch
    {
        public HashSet<DevelopmentEcosystem> Ecosystems { get; } = [];

        public HashSet<string> Indicators { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
