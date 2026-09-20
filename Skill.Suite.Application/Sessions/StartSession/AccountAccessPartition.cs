namespace Skill.Suite.Application.Sessions.StartSession;

using Skill.Suite.Domain.Competitors;

/// <summary>
/// The competitors a provisioning stage may act on, and the usernames it is leaving alone.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="SessionProvisioningFailure"/> on purpose: a skipped competitor is one nothing was
/// attempted for, so it carries no message from the external system and must not read as work that could not
/// be completed.
/// </remarks>
internal sealed record AccountAccessPartition(
    IReadOnlyList<Competitor> Provisionable,
    IReadOnlyList<string> Skipped);
