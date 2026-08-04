namespace Skill.Suite.Application.TestRuns.MyTestRuns;

/// <summary>
/// Coarse pass-rate bucket shown to competitors instead of an exact score.
/// Computed over the fixture's <c>Summary_</c>-prefixed unit tests so competitors
/// can't reverse-engineer the full test corpus.
/// </summary>
public enum ScoreBucket
{
    None = 0,
    VeryLow = 1,
    Low = 2,
    High = 3,
    VeryHigh = 4,
}
