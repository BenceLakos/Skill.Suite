namespace Skill.Suite.Application.Sessions.StopSession;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Suspends a session: it stops accepting pushes, the competitors lose access to their repositories, and the
/// session's docker services are stopped.
/// </summary>
/// <remarks>
/// The reversible counterpart to closing. Nothing is deleted — repositories, their history and the
/// competitors' database access all stay exactly as they were — so starting the session again puts the
/// competition back where it was. That is also the repair path for a start that half-succeeded: stop it,
/// then start it again.
/// <para>
/// No step aborts another. A repository whose grant cannot be revoked or a container the daemon will not
/// stop is recorded against the item it happened to and reported in
/// <see cref="StopSessionResult.Failed"/>, while the rest of the session is stopped regardless — the
/// alternative is a session that keeps accepting work because one container was unreachable.
/// </para>
/// <para>
/// <paramref name="Progress"/> is optional and receives a report as each stage advances, on the same terms
/// as <see cref="StartSession.StartSessionCommand"/>: carrying a callback on a request is only acceptable
/// because this command never leaves the process.
/// </para>
/// </remarks>
public sealed record StopSessionCommand(
    Guid Id,
    IProgress<SessionProvisioningProgress>? Progress = null) : IRequest<Result<StopSessionResult>>;
