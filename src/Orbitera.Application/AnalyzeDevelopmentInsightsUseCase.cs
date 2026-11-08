using Orbitera.Domain;

namespace Orbitera.Application;

public sealed class AnalyzeDevelopmentInsightsUseCase(
    StorageAnalysisSession session,
    DevelopmentInsightsAnalyzer analyzer)
{
    public DevelopmentInsightsResult? Execute(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = session.LatestRoot;
        return root is null ? null : analyzer.Analyze(root, cancellationToken);
    }
}
