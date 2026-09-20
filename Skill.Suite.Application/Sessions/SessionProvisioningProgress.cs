namespace Skill.Suite.Application.Sessions;

/// <summary>
/// How far starting or stopping a session has got, reported as it happens rather than at the end.
/// </summary>
/// <remarks>
/// Both commands are a network conversation per competitor and run inside the request, so without this the
/// admin watches a disabled button for a minute with no way to tell a slow git host from a stuck one.
/// </remarks>
public sealed record SessionProvisioningProgress(SessionProvisioningStage Stage, int Completed, int Total)
{
    /// <summary>The full bar, and the upper bound of <see cref="Percent"/>.</summary>
    private const int PercentScale = 100;

    /// <summary>A stage that reports no count reports zero for both halves of the fraction.</summary>
    private const int Uncounted = 0;

    /// <summary>A stage whose work cannot be counted up front, shown as a moving bar with no figure.</summary>
    public bool IsIndeterminate => Total <= Uncounted;

    /// <summary>
    /// Completion as a whole percentage, 0 to 100.
    /// </summary>
    /// <remarks>
    /// Rounds to the nearest whole percent, except that a stage with work left never reaches 100: with two
    /// hundred competitors the last one rounds up to a bar that reads as finished while a repository is
    /// still being created, which is exactly the "is it stuck?" question this report exists to answer.
    /// </remarks>
    public int Percent
    {
        get
        {
            if (IsIndeterminate)
                return Uncounted;

            if (Completed >= Total)
                return PercentScale;

            var rounded = (int)Math.Round(
                (double)Completed * PercentScale / Total, MidpointRounding.AwayFromZero);

            return Math.Clamp(rounded, Uncounted, PercentScale - 1);
        }
    }

    /// <summary>A stage that has started but has nothing countable to report.</summary>
    public static SessionProvisioningProgress Indeterminate(SessionProvisioningStage stage) =>
        new(stage, Uncounted, Uncounted);
}
