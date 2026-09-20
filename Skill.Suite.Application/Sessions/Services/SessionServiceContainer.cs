namespace Skill.Suite.Application.Sessions.Services;

/// <summary>One container of a session, identified by the only two things stopping it needs.</summary>
/// <param name="CompetitorUsername">Whose copy this is. Every container belongs to exactly one competitor.</param>
internal sealed record SessionServiceContainer(
    string ContainerName,
    string Image,
    string CompetitorUsername)
{
    /// <summary>
    /// What a failure against this container is filed under.
    /// </summary>
    /// <remarks>
    /// The image alone stopped being enough once one image is one container per competitor: "postgres:17
    /// failed" leaves the admin to work out which of twenty competitors is without a service.
    /// </remarks>
    public string Subject => $"{Image} ({CompetitorUsername})";
}
