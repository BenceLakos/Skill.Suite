namespace Skill.Suite.Domain.TestRuns;

/// <summary>
/// The reasons a run ends in a terminal state without having produced a verdict.
/// </summary>
/// <remarks>
/// Shared because the supersede reason is written from two places — the webhook handler cancels older runs
/// for the same competitor, and the executing handler notices its own cancellation. When those two strings
/// drifted apart the same event described itself two different ways depending on which side won the race.
/// </remarks>
public static class TestRunReasons
{
    /// <summary>An older run for the same competitor, replaced by a newer push.</summary>
    public const string Superseded = "Superseded by a newer submission for the same competitor.";

    /// <summary>
    /// An operator stopped the run from the UI.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Superseded"/> on purpose: superseded is routine and expected, while this one is
    /// a human decision that an expert may have to account for when a competitor asks what happened to a run.
    /// </remarks>
    public const string CancelledByOperator = "Cancelled by an operator.";

    /// <summary>
    /// The whole run exceeded the platform's backstop budget. Takes the budget in minutes.
    /// </summary>
    public const string TimedOutFormat =
        "The judgement run exceeded the {0:0.#} minute limit and was stopped. This usually means the "
        + "judgement image hung; the run was abandoned so later submissions could be processed.";

    /// <summary>
    /// The container exited successfully but produced nothing to score.
    /// </summary>
    /// <remarks>
    /// Almost always a submission that does not compile: <c>dotnet test</c> returns the same exit code for a
    /// compile error as for a failing test, so a judge that tolerates red tests reports both as success.
    /// </remarks>
    public const string NoResults =
        "The judgement container exited successfully but produced no test results. This usually means the "
        + "submission does not compile, or the judge image discovered no tests.";

    /// <summary>
    /// The repository URL the push advertised is not an absolute http(s) URL, so it was never handed to git.
    /// </summary>
    /// <remarks>
    /// Not a competitor's mistake: the git host builds this field, so a value that is not a URL means either a
    /// misconfigured host or a forged delivery. Recorded as a distinct reason because it needs an operator to
    /// look at the host, not a re-push.
    /// </remarks>
    public const string InvalidRepositoryUrl =
        "The push advertised a repository URL that is not an absolute http(s) URL, so it was refused before "
        + "cloning. This is a git host configuration problem, not a problem with the submission.";
}
