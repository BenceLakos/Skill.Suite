namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One container a session is to run, with every placeholder already resolved.
/// </summary>
/// <remarks>
/// The unit both start handlers loop over, which is what makes "which containers" a decision taken once.
/// Every one of these belongs to exactly one competitor: an image is one container per competitor, the same
/// way a session's database base name is one database per competitor.
/// </remarks>
/// <param name="Image">The reference the daemon is handed, already restated in its own terms.</param>
/// <param name="ConfiguredImage">
/// The reference the session stores, kept for logs and for the administrator: it is what they typed, and it
/// is what they would search the session for.
/// </param>
/// <param name="Network">
/// The docker network to attach the container to, or null to leave it on the daemon's default bridge as
/// every session service was before routing existed.
/// </param>
/// <param name="Host">
/// The hostname the reverse proxy answers for this container, or null when it is not routed. Not a URL: the
/// port a browser needs belongs to the proxy's publication, which only the caller's own address reveals.
/// </param>
internal sealed record PlannedSessionService(
    string ServiceNumber,
    string Image,
    string ConfiguredImage,
    string ContainerName,
    string CompetitorUsername,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<VolumeMount> Volumes,
    IReadOnlyList<PortMapping> PortMappings,
    string? Network,
    string? Host)
{
    /// <summary>
    /// What a failure against this container is filed under.
    /// </summary>
    /// <remarks>
    /// The image alone stopped being enough once one image is twenty containers: "postgres:17 failed" leaves
    /// the admin to work out which competitor is without a service.
    /// </remarks>
    public string Subject => $"{ConfiguredImage} ({CompetitorUsername})";
}
