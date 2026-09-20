namespace Skill.Suite.Application.Sessions.StartMarking;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Brings one competitor's docker services back up on a closed session, for an expert to mark from a named
/// machine, with every database-scoped setting pointed at that competitor's database as the SQL Server
/// administrator.
/// </summary>
/// <remarks>
/// The same images, the same per-competitor host ports and the same domain the competition used — the
/// containers differ by a <c>-marking</c> suffix on their names, a label saying so, the login the database
/// placeholders resolve to, and the one source address their route accepts. Connecting as the administrator
/// is the point: an expert has to see everything the competitor produced, and the competitor's own login is
/// bounded by the session's read and write flags, which on a read-only session is nothing worth marking.
/// <para>
/// One competitor at a time, and the marking machine's address is typed in each time, because nothing on the
/// platform knows where an expert is sitting. <paramref name="MarkingIpAddress"/> is what the reverse proxy
/// matches on, so it is the whole of what makes the domain lead to THIS competitor's container.
/// </para>
/// <para>
/// POINTING ONE MACHINE AT TWO COMPETITORS AT ONCE does not work and is not detected. Both runs would label
/// a router with the same hostname and the same address, and which of the two the proxy picks is a tie-break
/// nothing here controls — so stop a competitor's marking before starting another from the same machine. The
/// dialog says so; detecting it would mean asking the daemon to list containers, which is an abstraction
/// this does not otherwise need.
/// </para>
/// <para>
/// Only a CLOSED session may be marked. Draft and Active are obvious; Stopped is excluded because it is a
/// pause rather than an ending — the competition is expected to be started again from there, which would
/// bring the competition containers back onto the host ports the marking containers are publishing, and
/// would do it while an expert is halfway through marking.
/// </para>
/// <para>
/// Safe to re-run: a marking container that is already up is reported as running rather than failing on its
/// name, so re-submitting with a corrected address replaces nothing and the previous one must be stopped
/// first.
/// </para>
/// </remarks>
public sealed record StartMarkingCommand(
    Guid Id,
    Guid CompetitorId,
    string MarkingIpAddress,
    IProgress<SessionProvisioningProgress>? Progress = null) : IRequest<Result<StartMarkingResult>>;
