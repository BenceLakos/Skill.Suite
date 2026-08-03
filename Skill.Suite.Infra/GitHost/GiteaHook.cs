namespace Skill.Suite.Infra.GitHost;

/// <summary>A webhook as Gitea reports it. <c>config</c> is an untyped string map on the wire.</summary>
internal sealed record GiteaHook
{
    public long Id { get; init; }
    public bool Active { get; init; }
    public Dictionary<string, string>? Config { get; init; }
}
