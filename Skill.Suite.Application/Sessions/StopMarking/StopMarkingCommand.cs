namespace Skill.Suite.Application.Sessions.StopMarking;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Removes the marking containers of one competitor, or of the whole session, and nothing else.
/// </summary>
/// <remarks>
/// Removal rather than a stop, unlike stopping a session: marking has no resumed state to protect, and a
/// stopped container keeps its name and would be recreated by the next marking run anyway. Removing it frees
/// the host ports and the proxy route straight away — which is what lets the same marking machine be pointed
/// at the next competitor.
/// </remarks>
/// <param name="CompetitorId">
/// Whose marking containers to remove, or null for every marking container of the session. The per-competitor
/// form is the ordinary one: an expert finishing with one competitor must not take down another expert's run.
/// </param>
public sealed record StopMarkingCommand(
    Guid Id, Guid? CompetitorId = null) : IRequest<Result<StopMarkingResult>>;
