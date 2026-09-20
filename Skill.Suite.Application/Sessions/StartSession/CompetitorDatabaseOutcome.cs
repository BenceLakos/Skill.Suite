namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// What provisioning one competitor's database managed to do.
/// </summary>
/// <remarks>
/// Two values rather than one, because they are genuinely independent: a seed script that fails leaves a
/// database the competitor can still be granted access to, and that grant is what decides whether they can
/// work at all. Reporting the failure while counting the grant is the honest answer to "how many competitors
/// have a database they can reach" — collapsing the two would either hide the broken seed or claim the
/// competitor was left with nothing.
/// </remarks>
/// <param name="Granted">Whether the competitor ended up with the access the session asks for.</param>
internal sealed record CompetitorDatabaseOutcome(
    bool Granted, IReadOnlyList<SessionProvisioningFailure> Failures);
