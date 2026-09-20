namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// How a stage's containers fared, and which of them are actually up.
/// </summary>
/// <remarks>
/// <see cref="Running"/> is not decoration on top of the count: marking reports an address per container to
/// connect to, and an address for a container that failed to start is worse than no address at all.
/// </remarks>
internal sealed record SessionServiceRunOutcome(
    SessionProvisioningStageOutcome Outcome,
    IReadOnlyList<PlannedSessionService> Running);
