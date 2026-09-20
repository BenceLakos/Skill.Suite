namespace Skill.Suite.Application.Sessions;

/// <summary>
/// How much of one stage's work got through, and what did not.
/// </summary>
/// <remarks>
/// <see cref="Succeeded"/> is not the item count minus the failure count. One item can produce more than one
/// failure — a competitor whose seed script failed and whose grant then failed too — and a stage can fail
/// once in a way that takes the rest of its items with it, so the two numbers are reported independently
/// rather than derived from one another.
/// </remarks>
internal sealed record SessionProvisioningStageOutcome(
    int Succeeded, IReadOnlyList<SessionProvisioningFailure> Failures);
