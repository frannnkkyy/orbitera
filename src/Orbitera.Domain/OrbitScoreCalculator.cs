using System.Globalization;

namespace Orbitera.Domain;

public static class OrbitScoreCalculator
{
    private const int SpaceWeight = 40;
    private const int GrowthWeight = 20;
    private const int DuplicateWeight = 15;
    private const int RegenerableWeight = 15;
    private const int ChangesWeight = 10;

    public static OrbitScoreResult? Calculate(OrbitScoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);

        var factors = new List<OrbitScoreFactor>();
        var capacity = input.TotalCapacityBytes;

        if (capacity is > 0 && input.AvailableBytes is long availableBytes)
        {
            var freePercent = (double)availableBytes / capacity.Value * 100;
            factors.Add(new OrbitScoreFactor(
                "Available space",
                SpaceWeight,
                LinearScore(freePercent, 5, 25),
                $"{FormatPercent(freePercent)} of the volume is free; 5% or less scores 0 and 25% or more scores 100."));
        }

        if (input.MonthlyGrowthPercent is double growthPercent)
        {
            factors.Add(new OrbitScoreFactor(
                "Growth trend",
                GrowthWeight,
                LinearScore(Math.Max(0, growthPercent), 10, 0),
                $"{FormatPercent(growthPercent)} net storage growth over the measured monthly interval; no growth scores 100 and 10% or more scores 0."));
        }

        if (capacity is > 0 && input.DuplicateBytes is long duplicateBytes)
        {
            var duplicatePercent = (double)duplicateBytes / capacity.Value * 100;
            factors.Add(new OrbitScoreFactor(
                "Identified duplicates",
                DuplicateWeight,
                LinearScore(duplicatePercent, 5, 0),
                $"{FormatPercent(duplicatePercent)} of volume capacity is identified as duplicate data; 0% scores 100 and 5% or more scores 0."));
        }

        if (capacity is > 0 && input.RegenerableBytes is long regenerableBytes)
        {
            var regenerablePercent = (double)regenerableBytes / capacity.Value * 100;
            factors.Add(new OrbitScoreFactor(
                "Regenerable data",
                RegenerableWeight,
                LinearScore(regenerablePercent, 10, 0),
                $"{FormatPercent(regenerablePercent)} of volume capacity is classified as potentially regenerable; 0% scores 100 and 10% or more scores 0."));
        }

        if (capacity is > 0 && input.ChangedBytes is long changedBytes)
        {
            var changedPercent = (double)changedBytes / capacity.Value * 100;
            factors.Add(new OrbitScoreFactor(
                "Changes between scans",
                ChangesWeight,
                LinearScore(changedPercent, 20, 1),
                $"{FormatPercent(changedPercent)} of volume capacity changed between the compared snapshots; up to 1% scores 100 and 20% or more scores 0."));
        }

        if (factors.Count == 0)
        {
            return null;
        }

        var knownWeight = factors.Sum(factor => factor.Weight);
        var weightedScore = factors.Sum(factor => factor.Score * factor.Weight);
        var score = (int)Math.Round((double)weightedScore / knownWeight, MidpointRounding.AwayFromZero);

        return new OrbitScoreResult(score, knownWeight, factors);
    }

    private static int LinearScore(double value, double zeroScoreAt, double fullScoreAt)
    {
        var score = fullScoreAt > zeroScoreAt
            ? 100 * (value - zeroScoreAt) / (fullScoreAt - zeroScoreAt)
            : 100 * (zeroScoreAt - value) / (zeroScoreAt - fullScoreAt);

        return (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
    }

    private static void ValidateInput(OrbitScoreInput input)
    {
        if (input.TotalCapacityBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Total capacity must be positive when provided.");
        }

        if (input.AvailableBytes is < 0 ||
            (input.TotalCapacityBytes is long capacity &&
             input.AvailableBytes is long available &&
             available > capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Available bytes must be between zero and total capacity.");
        }

        ValidateNonNegative(input.DuplicateBytes, nameof(input.DuplicateBytes));
        ValidateNonNegative(input.RegenerableBytes, nameof(input.RegenerableBytes));
        ValidateNonNegative(input.ChangedBytes, nameof(input.ChangedBytes));
    }

    private static void ValidateNonNegative(long? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Byte counts cannot be negative.");
        }
    }

    private static string FormatPercent(double value) =>
        $"{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
}
