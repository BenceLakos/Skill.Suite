namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Administrative operations against the git host (Gitea): organisations, repositories and webhooks.
/// </summary>
/// <remarks>
/// Separate from <see cref="IGitClient"/>, which speaks the git wire protocol to move commits. This one
/// speaks the host's REST API to create the containers those commits live in. Every method is idempotent:
/// provisioning runs over a network for N competitors and is restartable, so "already exists" is a success,
/// not a conflict.
/// </remarks>
public interface IGitHostClient
{
    /// <summary>Creates the organisation if it does not already exist.</summary>
    Task EnsureOrganizationAsync(
        EnsureOrganizationRequest request, CancellationToken cancellationToken);

    /// <summary>Creates the template repository, empty and flagged as a template, if it does not exist.</summary>
    Task EnsureTemplateRepositoryAsync(
        EnsureRepositoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the template repository into a new repository, including its git history.
    /// </summary>
    /// <returns>The clone URL of the new repository.</returns>
    /// <remarks>
    /// Verifies the result rather than trusting the response. The host reports success while producing an
    /// empty repository when the template's content is not yet visible to it, and a competitor handed an
    /// empty repository at the start of a competition has no way to recover the time.
    /// </remarks>
    Task<string> GenerateRepositoryFromTemplateAsync(
        GenerateRepositoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gives the user write access to the repository, or leaves an equivalent grant alone.
    /// </summary>
    /// <remarks>
    /// Not optional, and not a convenience. The session's repositories live in a private organisation the
    /// competitor is not a member of, so without this grant the repository they were provisioned exists and
    /// is invisible to them: cloning it asks for credentials their own account cannot satisfy. Write rather
    /// than read, because the whole point is for them to push.
    /// </remarks>
    Task EnsureCollaboratorAsync(
        RepositoryReference repository,
        string username,
        BasicCredential credential,
        CancellationToken cancellationToken);

    /// <summary>Takes the user's access to the repository away again. Idempotent.</summary>
    /// <remarks>
    /// The repository and its history are untouched: only the grant goes. Stopping a session must not cost
    /// anybody the submissions a marking dispute is settled from.
    /// </remarks>
    Task<AccountRemoval> RemoveCollaboratorAsync(
        RepositoryReference repository,
        string username,
        BasicCredential credential,
        CancellationToken cancellationToken);

    /// <summary>
    /// Installs a push webhook on the organisation, replacing any hook already pointing at the same URL.
    /// </summary>
    /// <remarks>
    /// One hook for the organisation rather than one per repository: it covers repositories created later,
    /// and re-running provisioning does not accumulate duplicates that would each fire a run.
    /// </remarks>
    Task EnsureOrganizationWebhookAsync(
        EnsureWebhookRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the organisation's push webhook off again, if one points at the given URL. Idempotent.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="EnsureOrganizationWebhookAsync"/>, and the same delete it already
    /// performs before reinstalling. Needed on its own because a session can stop being judged: clearing its
    /// judgement image and starting it again has to leave the organisation without a hook, or every push
    /// keeps being delivered to an endpoint whose only possible answer is a refusal.
    /// </remarks>
    Task RemoveOrganizationWebhookAsync(
        RemoveWebhookRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Blocks until the repository reports content, or the timeout elapses.
    /// </summary>
    /// <returns><see langword="true"/> when the repository has content.</returns>
    /// <remarks>
    /// Takes the credential because competitor repositories are private: polling anonymously cannot tell an
    /// empty repository from one it is not allowed to see, and would report every repository as empty.
    /// </remarks>
    Task<bool> WaitForRepositoryContentAsync(
        RepositoryReference repository,
        BasicCredential credential,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>Creates the competitor's user account if it does not already exist.</summary>
    /// <remarks>
    /// An existing account's password is deliberately left alone: the competitor may already be working with
    /// it, and re-running provisioning must not lock them out of their own repository.
    /// </remarks>
    Task<AccountProvisioning> EnsureUserAsync(
        EnsureGitHostUserRequest request, CancellationToken cancellationToken);

    /// <summary>Lists every user account on the host, for bulk status evaluation.</summary>
    Task<IReadOnlyCollection<string>> ListUsernamesAsync(
        BasicCredential credential, CancellationToken cancellationToken);

    /// <summary>Whether the user still owns any repository.</summary>
    /// <remarks>
    /// Removal refuses when they do. Deleting the user would take their repositories with them, and a
    /// competitor's submission history is the evidence a marking dispute is settled from.
    /// </remarks>
    Task<bool> HasRepositoriesAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken);

    /// <summary>Deletes the user account. Destructive and not recoverable.</summary>
    Task<AccountRemoval> DeleteUserAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken);
}

/// <summary>Identifies a repository on the host.</summary>
public sealed record RepositoryReference(string Owner, string Name);

public sealed record EnsureOrganizationRequest(
    string Name,
    string? Description,
    BasicCredential Credential);

public sealed record EnsureRepositoryRequest(
    RepositoryReference Repository,
    string? Description,
    BasicCredential Credential);

public sealed record GenerateRepositoryRequest(
    RepositoryReference Template,
    RepositoryReference Target,
    string DefaultBranch,
    BasicCredential Credential);

public sealed record EnsureWebhookRequest(
    string Organization,
    string TargetUrl,
    string Secret,
    string BranchFilter,
    BasicCredential Credential);

public sealed record RemoveWebhookRequest(
    string Organization,
    string TargetUrl,
    BasicCredential Credential);
