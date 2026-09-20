namespace Skill.Suite.Application.Sessions.StopMarking;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Removes the marking containers a session's marking run created, and nothing else.
/// </summary>
/// <remarks>
/// Removal rather than a stop, unlike stopping a session: marking has no resumed state to protect, and a
/// stopped container keeps its name and would be recreated by the next marking run anyway. Removing it frees
/// the host ports as soon as the marking is done, which is what lets a second session be marked on the same
/// machine.
/// </remarks>
public sealed record StopMarkingCommand(Guid Id) : IRequest<Result<StopMarkingResult>>;
