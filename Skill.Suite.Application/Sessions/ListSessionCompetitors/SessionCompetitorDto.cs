namespace Skill.Suite.Application.Sessions.ListSessionCompetitors;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One competitor enrolled in a session, as the admin needs them listed to act on.
/// </summary>
/// <param name="IpAddress">
/// Their workstation, which is what the competition's proxy routes them by. Shown next to the marking
/// address field so it is obvious the two are different machines.
/// </param>
/// <param name="MobileIpAddress">
/// Their second device, or null. Shown because it keeps reaching the container while their work is being
/// marked, which is a thing the expert entering the marking address should be able to see.
/// </param>
/// <param name="Ordinal">
/// Their stable position in the session — the number every host port of theirs is counted up by.
/// </param>
public sealed record SessionCompetitorDto(
    Guid CompetitorId,
    string Username,
    string FullName,
    string IpAddress,
    string? MobileIpAddress,
    int Ordinal,
    SessionProvisionStatus ProvisionStatus);
