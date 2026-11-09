using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public sealed class CleanupPlanFactoryTests
{
    [Fact]
    public void CreatesPlanOnlyFromSelectedRecoverableCandidates()
    {
        var candidates = new[]
        {
            Candidate("C:\\workspace\\app\\bin", "bin", 100, true, DevelopmentRiskLevel.Low),
            Candidate("C:\\workspace\\app\\dist", "dist", 250, true, DevelopmentRiskLevel.High),
            Candidate("C:\\workspace\\repo\\.git", ".git", 500, false, DevelopmentRiskLevel.High)
        };

        var plan = CleanupPlanFactory.Create(
            candidates,
            [@"C:\workspace\app\dist"],
            DateTime.UnixEpoch);

        Assert.Single(plan.Items);
        Assert.Equal(@"C:\workspace\app\dist", plan.Items[0].Path);
        Assert.Equal(250, plan.EstimatedRecoverableBytes);
        Assert.Contains(plan.Items, item => item.RiskLevel == DevelopmentRiskLevel.High);
        Assert.Equal(DateTime.UnixEpoch, plan.CreatedUtc);
        Assert.True(plan.IsReadOnly);
    }

    [Fact]
    public void RejectsPathsThatAreNotCandidates()
    {
        Assert.Throws<ArgumentException>(() =>
            CleanupPlanFactory.Create(
                [Candidate(@"C:\workspace\app\bin", "bin", 100, true, DevelopmentRiskLevel.Low)],
                [@"C:\workspace\app\obj"]));
    }

    [Fact]
    public void RejectsCandidatesRulesMarkAsNotRecoverable()
    {
        Assert.Throws<ArgumentException>(() =>
            CleanupPlanFactory.Create(
                [Candidate(@"C:\workspace\repo\.git", ".git", 500, false, DevelopmentRiskLevel.High)],
                [@"C:\workspace\repo\.git"]));
    }

    private static DevelopmentInsightRecommendation Candidate(
        string path,
        string name,
        long bytes,
        bool recoverable,
        DevelopmentRiskLevel risk) =>
        new(
            "app",
            @"C:\workspace\app",
            name,
            path,
            bytes,
            DevelopmentEcosystem.DotNet,
            DevelopmentInsightCategory.BuildArtifact,
            risk,
            recoverable,
            $"{name} purpose",
            $"{name} impact");
}
