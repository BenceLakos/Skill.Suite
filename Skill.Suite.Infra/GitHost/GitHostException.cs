namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// A git-host operation failed. The message is written to be shown to an admin, because it lands in
/// <c>SessionCompetitor.ProvisionError</c> and is the only thing they have to act on.
/// </summary>
public sealed class GitHostException(string message) : Exception(message);
