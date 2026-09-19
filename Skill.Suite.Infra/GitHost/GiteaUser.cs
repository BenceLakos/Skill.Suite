namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// The subset of Gitea's user representation the competitors page depends on.
/// </summary>
/// <remarks>
/// <c>login</c>, not <c>username</c>: the admin user listing reports the account name under the former, and
/// the latter is absent. Binding the wrong one yields a list of nulls and every competitor shown as missing.
/// </remarks>
internal sealed record GiteaUser
{
    public string? Login { get; init; }
}
