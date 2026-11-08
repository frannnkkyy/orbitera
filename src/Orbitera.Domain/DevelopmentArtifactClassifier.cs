namespace Orbitera.Domain;

public sealed class DevelopmentArtifactClassifier(DevelopmentInsightsRules rules)
{
    public IReadOnlyList<ClassifiedDevelopmentArtifact> Classify(
        StorageNode scanRoot,
        IReadOnlyList<DetectedDevelopmentProject> projects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);
        ArgumentNullException.ThrowIfNull(projects);

        var projectNodes = projects
            .Select(project => (Project: project, Node: FindDirectory(scanRoot, project.RootPath)))
            .Where(item => item.Node is not null)
            .ToArray();
        var artifacts = new List<ClassifiedDevelopmentArtifact>();

        foreach (var directory in EnumerateDirectories(scanRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rule = rules.ArtifactRules.FirstOrDefault(candidate =>
                directory.Name.Equals(candidate.DirectoryName, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                continue;
            }

            var project = FindOwningProject(directory, projectNodes, rule);

            if (project is null || !HasAdditionalMarkers(project.Value.Node!, rule.AdditionalRequiredMarkers))
            {
                continue;
            }

            artifacts.Add(new ClassifiedDevelopmentArtifact(directory, project.Value.Project, rule));
        }

        var topLevelArtifacts = artifacts
            .OrderBy(artifact => GetDepth(artifact.Node))
            .ToArray();
        var selectedArtifacts = new List<ClassifiedDevelopmentArtifact>();

        foreach (var artifact in topLevelArtifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (selectedArtifacts.Any(parent => IsSameOrAncestor(parent.Node, artifact.Node)))
            {
                continue;
            }

            selectedArtifacts.Add(artifact);
        }

        return selectedArtifacts
            .OrderByDescending(artifact => artifact.Node.SizeBytes)
            .ThenBy(artifact => artifact.Node.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (DetectedDevelopmentProject Project, StorageNode? Node)? FindOwningProject(
        StorageNode directory,
        IEnumerable<(DetectedDevelopmentProject Project, StorageNode? Node)> projects,
        DevelopmentInsightRule rule)
    {
        (DetectedDevelopmentProject Project, StorageNode? Node)? match = null;
        var bestDepth = -1;

        foreach (var candidate in projects)
        {
            if (candidate.Node is null ||
                !candidate.Project.Ecosystems.Contains(rule.Ecosystem) ||
                !IsSameOrAncestor(candidate.Node, directory))
            {
                continue;
            }

            var depth = GetDepth(candidate.Node);

            if (depth > bestDepth)
            {
                bestDepth = depth;
                match = candidate;
            }
        }

        return match;
    }

    private static bool HasAdditionalMarkers(StorageNode projectRoot, IReadOnlyList<string>? markers)
    {
        if (markers is null || markers.Count == 0)
        {
            return true;
        }

        return projectRoot.Children.Any(child =>
            !child.IsDirectory &&
            markers.Any(marker => child.Name.Equals(marker, StringComparison.OrdinalIgnoreCase)));
    }

    private static StorageNode? FindDirectory(StorageNode root, string path)
    {
        foreach (var directory in EnumerateDirectories(root))
        {
            if (string.Equals(directory.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return directory;
            }
        }

        return null;
    }

    private static bool IsSameOrAncestor(StorageNode ancestor, StorageNode node)
    {
        for (StorageNode? current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetDepth(StorageNode node)
    {
        var depth = 0;
        for (StorageNode? current = node; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
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
}
