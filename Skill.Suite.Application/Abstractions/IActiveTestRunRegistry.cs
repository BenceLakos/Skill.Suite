namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Tracks the in-flight test run for each competitor. The webhook handler consults this
/// before persisting a new submission so that a fresh push for a competitor cancels — and
/// stops the docker container of — that competitor's previous unfinished run.
/// </summary>
public interface IActiveTestRunRegistry
{
    /// <summary>
    /// Registers the run as active for <paramref name="competitorId"/>. If another run was
    /// already active for the same competitor, its CancellationTokenSource is signalled
    /// (which the container runner translates into a <c>docker stop</c>).
    /// </summary>
    /// <returns>true if a previous run was cancelled.</returns>
    bool Register(Guid testRunId, Guid competitorId, CancellationTokenSource cts);

    /// <summary>
    /// Cancels the active run for <paramref name="competitorId"/>, if any. Returns true
    /// when a cancellation was actually signalled.
    /// </summary>
    bool CancelForCompetitor(Guid competitorId);

    /// <summary>Removes the run from the registry. Idempotent.</summary>
    void Unregister(Guid testRunId);
}
