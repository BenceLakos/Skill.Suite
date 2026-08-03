namespace Skill.Suite.Marker.Model;

/// <summary>
/// Line-coverage totals for one part.
/// </summary>
/// <param name="LinesCovered">Coverable lines hit at least once.</param>
/// <param name="LinesTotal">Coverable lines.</param>
public readonly record struct CoverageStats(int LinesCovered, int LinesTotal)
{
    /// <summary>Covered fraction in 0..1; 0 when there is nothing to cover.</summary>
    public double Rate => LinesTotal == 0 ? 0.0 : (double)LinesCovered / LinesTotal;

    /// <summary>Adds another part's totals to this one.</summary>
    public CoverageStats Add(CoverageStats other) =>
        new(LinesCovered + other.LinesCovered, LinesTotal + other.LinesTotal);
}
