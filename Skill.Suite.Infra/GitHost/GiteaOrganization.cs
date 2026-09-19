namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// The subset of Gitea's organisation representation the registry listing depends on.
/// </summary>
/// <remarks>
/// The admin listing reports the organisation under <c>name</c>; <c>username</c> is a deprecated alias kept
/// for older clients and is not guaranteed to be populated. <see cref="Name"/> therefore wins, with
/// <see cref="Username"/> only as a fallback for a host that still answers the old way.
/// </remarks>
internal sealed record GiteaOrganization
{
    public string? Name { get; init; }

    public string? Username { get; init; }
}
