namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Abstractions;

/// <summary>
/// An <see cref="IGitHostClient"/> that records the webhook calls made to it and refuses every other one.
/// </summary>
/// <remarks>
/// Deliberately throws for the operations the test does not exercise rather than returning a benign default:
/// the thing being asserted is that exactly one of install or remove happened, and a silently-satisfied call
/// would let a change that also created repositories pass unnoticed.
/// </remarks>
internal sealed class RecordingGitHostClient : IGitHostClient
{
    public List<EnsureWebhookRequest> Installed { get; } = [];

    public List<RemoveWebhookRequest> Removed { get; } = [];

    public Task EnsureOrganizationWebhookAsync(
        EnsureWebhookRequest request, CancellationToken cancellationToken)
    {
        Installed.Add(request);
        return Task.CompletedTask;
    }

    public Task RemoveOrganizationWebhookAsync(
        RemoveWebhookRequest request, CancellationToken cancellationToken)
    {
        Removed.Add(request);
        return Task.CompletedTask;
    }

    public Task EnsureOrganizationAsync(
        EnsureOrganizationRequest request, CancellationToken cancellationToken) => throw NotExercised();

    public Task EnsureTemplateRepositoryAsync(
        EnsureRepositoryRequest request, CancellationToken cancellationToken) => throw NotExercised();

    public Task<string> GenerateRepositoryFromTemplateAsync(
        GenerateRepositoryRequest request, CancellationToken cancellationToken) => throw NotExercised();

    public Task EnsureCollaboratorAsync(
        RepositoryReference repository,
        string username,
        BasicCredential credential,
        CancellationToken cancellationToken) => throw NotExercised();

    public Task<AccountRemoval> RemoveCollaboratorAsync(
        RepositoryReference repository,
        string username,
        BasicCredential credential,
        CancellationToken cancellationToken) => throw NotExercised();

    public Task<bool> WaitForRepositoryContentAsync(
        RepositoryReference repository,
        BasicCredential credential,
        TimeSpan timeout,
        CancellationToken cancellationToken) => throw NotExercised();

    public Task<AccountProvisioning> EnsureUserAsync(
        EnsureGitHostUserRequest request, CancellationToken cancellationToken) => throw NotExercised();

    public Task<IReadOnlyCollection<string>> ListUsernamesAsync(
        BasicCredential credential, CancellationToken cancellationToken) => throw NotExercised();

    public Task<bool> HasRepositoriesAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken) => throw NotExercised();

    public Task<AccountRemoval> DeleteUserAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken) => throw NotExercised();

    private static NotSupportedException NotExercised() =>
        new("This git host operation is not part of what the test under way is asserting.");
}
