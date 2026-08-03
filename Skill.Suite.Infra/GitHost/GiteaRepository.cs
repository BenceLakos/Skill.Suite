namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// The subset of Gitea's repository representation provisioning depends on.
/// </summary>
/// <remarks>
/// <see cref="Empty"/> is the important one: it is how a silently-failed template copy is detected. Gitea
/// returns 201 with an otherwise normal-looking repository when the copy produced nothing.
/// </remarks>
internal sealed record GiteaRepository
{
    public long Id { get; init; }
    public string? FullName { get; init; }
    public string? CloneUrl { get; init; }
    public string? DefaultBranch { get; init; }
    public bool Empty { get; init; }
    public bool Template { get; init; }
}
