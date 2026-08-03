namespace Skill.Suite.Marker.Output;

/// <summary>
/// One row of the CIS report: whether an aspect is satisfied, and the evidence.
/// </summary>
/// <param name="AspectId">The aspect id.</param>
/// <param name="TestsMatched">Test cases that claimed this aspect.</param>
/// <param name="TestsPassed">How many of them passed.</param>
/// <remarks>
/// Carries no marks. Points are computed downstream in CIS from these yes/no answers, which is why the
/// row also reports the counts: a <c>no</c> caused by "no test covered this" is a scheme problem, while a
/// <c>no</c> with 5 matched and 4 passed is a competitor result.
/// </remarks>
public sealed record AspectResult(string AspectId, int TestsMatched, int TestsPassed)
{
    /// <summary>
    /// <c>yes</c> only when at least one test matched and every matched test passed. A skipped or errored
    /// test is not a pass, so it makes the aspect <c>no</c>.
    /// </summary>
    public bool Satisfied => TestsMatched > 0 && TestsPassed == TestsMatched;

    /// <summary>The literal written to the CSV.</summary>
    public string Result => Satisfied ? "yes" : "no";
}
