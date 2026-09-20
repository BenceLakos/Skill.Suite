namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Runs the long-lived docker services a session depends on — a database, a broker, whatever the session was
/// configured with — as opposed to <see cref="IContainerRunner"/>, which runs one judgement to completion.
/// </summary>
/// <remarks>
/// There is exactly one instance of each service per session, because the ports it publishes are fixed host
/// ports and a second copy would collide on them. That is why the operations here are idempotent rather than
/// create/delete: session start may be retried, and it has to be able to say "this is already up" instead of
/// failing on a name or port that is in use by the very thing it was asked to ensure.
/// </remarks>
public interface IContainerServiceManager
{
    /// <summary>
    /// Starts the service detached if no container with that name is running. A same-named container that
    /// exists but is not running is removed and recreated. Idempotent.
    /// </summary>
    Task<ContainerServiceStart> EnsureRunningAsync(ContainerServiceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Stops the container if it is running, and leaves it in place. Idempotent.
    /// </summary>
    /// <remarks>
    /// Stopping rather than removing, because a stopped session is meant to be started again:
    /// <see cref="EnsureRunningAsync"/> recreates a stopped container on the next start, so the service
    /// comes back with whatever image and ports the session carries by then.
    /// </remarks>
    Task<ContainerServiceStop> StopAsync(string containerName, CancellationToken cancellationToken);

    /// <summary>
    /// Force-removes every container carrying ALL of <paramref name="labels"/>. Returns how many were removed.
    /// </summary>
    /// <remarks>
    /// A set rather than one pair, because one label is not always narrow enough to be safe. Removing a
    /// session's containers is one label; removing the marking containers of ONE competitor within a session
    /// is the marking label and the competitor label together, and matching either alone would take down
    /// another expert's marking run or the whole session.
    /// <para>
    /// An empty set is refused rather than treated as "everything": the one thing this must never do is
    /// remove containers that have nothing to do with the caller.
    /// </para>
    /// </remarks>
    Task<int> RemoveByLabelsAsync(
        IReadOnlyDictionary<string, string> labels, CancellationToken cancellationToken);
}
