using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public sealed class OrbitScoreCalculatorTests
{
    [Fact]
    public void CalculatesWeightedScoreFromAvailableFactorsAndExplainsEachFactor()
    {
        var result = OrbitScoreCalculator.Calculate(new OrbitScoreInput(
            TotalCapacityBytes: 1_000,
            AvailableBytes: 250,
            MonthlyGrowthPercent: 5,
            DuplicateBytes: 50,
            RegenerableBytes: 100,
            ChangedBytes: 100));

        Assert.NotNull(result);
        Assert.Equal(55, result.Score);
        Assert.Equal(100, result.CoveragePercent);
        Assert.Equal(5, result.Factors.Count);
        Assert.All(result.Factors, factor => Assert.False(string.IsNullOrWhiteSpace(factor.Explanation)));
    }

    [Fact]
    public void RenormalizesScoreAndReportsCoverageWhenFactorsAreUnavailable()
    {
        var result = OrbitScoreCalculator.Calculate(new OrbitScoreInput(
            TotalCapacityBytes: 1_000,
            AvailableBytes: 1_000,
            MonthlyGrowthPercent: null,
            DuplicateBytes: null,
            RegenerableBytes: null,
            ChangedBytes: null));

        Assert.NotNull(result);
        Assert.Equal(100, result.Score);
        Assert.Equal(40, result.CoveragePercent);
        Assert.Single(result.Factors);
    }

    [Fact]
    public void ReturnsNoScoreWhenThereIsNoCapacityEvidence()
    {
        var result = OrbitScoreCalculator.Calculate(new OrbitScoreInput(
            TotalCapacityBytes: null,
            AvailableBytes: null,
            MonthlyGrowthPercent: null,
            DuplicateBytes: null,
            RegenerableBytes: null,
            ChangedBytes: null));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RejectsAvailableSpaceOutsideCapacity(long availableBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrbitScoreCalculator.Calculate(new OrbitScoreInput(
                TotalCapacityBytes: 100,
                AvailableBytes: availableBytes,
                MonthlyGrowthPercent: null,
                DuplicateBytes: null,
                RegenerableBytes: null,
                ChangedBytes: null)));
    }
}
