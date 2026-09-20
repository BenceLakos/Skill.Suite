namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One container a session is to run, with every placeholder already resolved.
/// </summary>
/// <remarks>
/// The unit both start handlers loop over, which is what makes "how many containers" a decision taken once.
/// A shared service produces exactly one of these with <see cref="CompetitorUsername"/> null — the container
/// sessions have always started — and a per-competitor service produces one each.
/// </remarks>
/// <param name="Image">The reference the daemon is handed, already restated in its own terms.</param>
/// <param name="ConfiguredImage">
/// The reference the session stores, kept for logs and for the administrator: it is what they typed, and it
/// is what they would search the session for.
/// </param>
internal sealed record PlannedSessionService(
    string ServiceNumber,
    string Image,
    string ConfiguredImage,
    string ContainerName,
    string? CompetitorUsername,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<VolumeMount> Volumes,
    IReadOnlyList<PortMapping> PortMappings)
{
    /// <summary>
    /// What a failure against this container is filed under.
    /// </summary>
    /// <remarks>
    /// The image alone stopped being enough once one image can be twenty containers: "postgres:17 failed"
    /// leaves the admin to work out which competitor is without a database.
    /// </remarks>
    public string Subject =>
        CompetitorUsername is null ? ConfiguredImage : $"{ConfiguredImage} ({CompetitorUsername})";
}
