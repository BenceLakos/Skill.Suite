namespace Skill.Suite.Application.Sessions.Services;

/// <summary>One container of a session, identified by the only two things stopping it needs.</summary>
/// <param name="CompetitorUsername">Whose copy this is, or null for a shared service.</param>
internal sealed record SessionServiceContainer(
    string ContainerName,
    string Image,
    string? CompetitorUsername)
{
    /// <summary>What a failure against this container is filed under.</summary>
    public string Subject => CompetitorUsername is null ? Image : $"{Image} ({CompetitorUsername})";
}
