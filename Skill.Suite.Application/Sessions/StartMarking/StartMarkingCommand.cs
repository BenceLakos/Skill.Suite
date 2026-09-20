namespace Skill.Suite.Application.Sessions.StartMarking;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Brings a closed session's docker services back up for manual marking, one set per competitor, with every
/// database-scoped setting pointed at that competitor's database as the SQL Server administrator.
/// </summary>
/// <remarks>
/// The same images, the same placeholders and the same per-competitor host ports the competition used — the
/// containers only differ by a <c>-marking</c> suffix on their names, a label saying so, and the login the
/// database placeholders resolve to. That is what makes the marking setup usable on its own: an expert opens
/// the service for a competitor and sees that competitor's data, without the competitor's own grants, which
/// on a read-only session would have shown them nothing worth marking.
/// <para>
/// Only a CLOSED session may be marked. Draft and Active are obvious; Stopped is excluded because it is a
/// pause rather than an ending — the competition is expected to be started again from there, which would
/// bring the competition containers back onto the host ports the marking containers are publishing, and
/// would do it while an expert is halfway through marking.
/// </para>
/// <para>
/// Safe to re-run, like starting a session: a marking container that is already up is reported as running
/// rather than failing on its name.
/// </para>
/// </remarks>
public sealed record StartMarkingCommand(
    Guid Id,
    IProgress<SessionProvisioningProgress>? Progress = null) : IRequest<Result<StartMarkingResult>>;
