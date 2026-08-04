namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Clones a remote git repository into a target directory. Implementations shell out to
/// the local <c>git</c> binary — handlers stay free of process details.
/// </summary>
public interface IGitClient
{
    Task CloneAsync(GitCloneRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a directory's contents as the initial commit of an empty repository.
    /// </summary>
    /// <remarks>
    /// Used once per session to seed the template repository, not once per competitor — the host copies the
    /// template server-side after that. The source directory is read, never modified: the repository is
    /// assembled in a scratch directory so a starter package kept under version control on the host does not
    /// acquire a second <c>.git</c>.
    /// </remarks>
    Task PushDirectoryAsync(GitPushDirectoryRequest request, CancellationToken cancellationToken);
}

public sealed record GitCloneRequest(
    string RepositoryUrl,
    string TargetDirectory,
    string? Branch,
    BasicCredential? Credential = null);

public sealed record GitPushDirectoryRequest(
    string RepositoryUrl,
    string SourceDirectory,
    string Branch,
    string CommitMessage,
    BasicCredential? Credential = null);
