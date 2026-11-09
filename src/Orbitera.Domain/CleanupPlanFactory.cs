using System.IO;

namespace Orbitera.Domain;

public static class CleanupPlanFactory
{
    public static CleanupPlan Create(
        IEnumerable<DevelopmentInsightRecommendation> candidates,
        IEnumerable<string> selectedPaths,
        DateTime? createdUtc = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(selectedPaths);

        var candidateMap = new Dictionary<string, DevelopmentInsightRecommendation>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            var path = NormalizePath(candidate.DirectoryPath);

            if (!candidateMap.TryAdd(path, candidate))
            {
                throw new ArgumentException($"Duplicate cleanup candidate path: {candidate.DirectoryPath}", nameof(candidates));
            }
        }

        var items = new List<CleanupPlanItem>();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedPath in selectedPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
            var path = NormalizePath(selectedPath);

            if (!selected.Add(path))
            {
                continue;
            }

            if (!candidateMap.TryGetValue(path, out var candidate))
            {
                throw new ArgumentException($"Selected path is not a cleanup candidate: {selectedPath}", nameof(selectedPaths));
            }

            if (!candidate.IsPotentiallyRecoverable)
            {
                throw new ArgumentException($"Candidate is not marked as potentially recoverable: {selectedPath}", nameof(selectedPaths));
            }

            items.Add(new CleanupPlanItem(
                candidate.ProjectName,
                candidate.DirectoryName,
                path,
                candidate.SizeBytes,
                candidate.Ecosystem,
                candidate.Category,
                candidate.RiskLevel,
                candidate.Purpose,
                candidate.DeletionImpact));
        }

        var estimatedBytes = items.Aggregate(0L, (total, item) => checked(total + item.SizeBytes));

        return new CleanupPlan(
            (createdUtc ?? DateTime.UtcNow).ToUniversalTime(),
            items,
            estimatedBytes);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
