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
    /// Force-removes every container carrying the label <paramref name="labelKey"/>=<paramref name="labelValue"/>.
    /// Returns how many were removed.
    /// </summary>
    Task<int> RemoveByLabelAsync(string labelKey, string labelValue, CancellationToken cancellationToken);
}
