namespace Skill.Suite.Marker.Map;

/// <summary>
/// The scoring constants, read from <c>marking-map.json</c>.
/// </summary>
/// <remarks>
/// The defaults reproduce the previously hard-coded formula exactly: a coverage ramp of
/// <c>(pct - 40) / 40</c> is floor 40 / ceil 80, and a mutation ramp of <c>(pct - 30) / 50</c> is
/// floor 30 / ceil 80. They are knobs to calibrate per session, not universal truths.
/// </remarks>
public sealed record ScoringOptions
{
    /// <summary>Line coverage percentage at or below which scaled coverage is 0.</summary>
    public double CoverageFloor { get; init; } = 40.0;

    /// <summary>Line coverage percentage at or above which scaled coverage is 1.</summary>
    public double CoverageCeil { get; init; } = 80.0;

    /// <summary>Mutation kill percentage at or below which the mutation score is 0.</summary>
    public double MutationFloor { get; init; } = 30.0;

    /// <summary>Mutation kill percentage at or above which the mutation score is 1.</summary>
    public double MutationCeil { get; init; } = 80.0;

    /// <summary>Weight of raw scaled coverage in the final quality.</summary>
    public double CoverageWeight { get; init; } = 0.30;

    /// <summary>Weight of the composite core term in the final quality.</summary>
    public double CoreWeight { get; init; } = 0.70;

    /// <summary>Returns every reason this configuration is unusable, or an empty list when it is valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (CoverageCeil <= CoverageFloor)
            problems.Add($"scoring.coverageCeil ({CoverageCeil}) must be greater than scoring.coverageFloor ({CoverageFloor}).");

        if (MutationCeil <= MutationFloor)
            problems.Add($"scoring.mutationCeil ({MutationCeil}) must be greater than scoring.mutationFloor ({MutationFloor}).");

        if (CoverageWeight < 0 || CoreWeight < 0)
            problems.Add("scoring.coverageWeight and scoring.coreWeight must not be negative.");

        if (CoverageWeight + CoreWeight <= 0)
            problems.Add("scoring.coverageWeight and scoring.coreWeight must not both be zero.");

        return problems;
    }
}
