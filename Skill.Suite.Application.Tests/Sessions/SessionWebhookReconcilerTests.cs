namespace Skill.Suite.Application.Tests.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions.StartSession;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// Whether starting a session puts a push webhook on its organisation, and whether it takes one off.
/// </summary>
/// <remarks>
/// The hook is the entire link between a competitor pushing and their submission being marked, so both
/// mistakes are expensive and neither is visible from the application: installing one on a session that
/// cannot judge turns every legitimate push into a failed delivery on the administrator's hook page, and
/// leaving one on a session whose judgement image has been cleared keeps that going indefinitely.
/// </remarks>
public sealed class SessionWebhookReconcilerTests
{
    private const string TargetUrl = "https://suite.example/webhooks/git";
    private const string BranchFilter = "main";
    private const string Secret = "A1B2C3";

    private static readonly BasicCredential Admin = new("admin", "password");

    private static Session Draft(string? judgementImage) =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: judgementImage,
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

    private static async Task<RecordingGitHostClient> ReconcileAsync(string? judgementImage)
    {
        var gitHost = new RecordingGitHostClient();

        await SessionWebhookReconciler.ReconcileAsync(
            gitHost,
            NullLogger.Instance,
            Draft(judgementImage),
            TargetUrl,
            BranchFilter,
            Secret,
            Admin,
            CancellationToken.None);

        return gitHost;
    }

    [Fact]
    public async Task ASessionWithAJudgementImageGetsTheHook()
    {
        var gitHost = await ReconcileAsync("judge:1");

        var installed = Assert.Single(gitHost.Installed);
        Assert.Empty(gitHost.Removed);
        Assert.Equal("round-1", installed.Organization);
        Assert.Equal(TargetUrl, installed.TargetUrl);
        Assert.Equal(Secret, installed.Secret);
        Assert.Equal(BranchFilter, installed.BranchFilter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ASessionWithNoJudgementImageDoesNotGetTheHook(string? judgementImage)
    {
        var gitHost = await ReconcileAsync(judgementImage);

        Assert.Empty(gitHost.Installed);
    }

    [Fact]
    public async Task ASessionWithNoJudgementImageHasAnyHookOfItsOwnRemoved()
    {
        // The repair path for a session that used to be judged: clearing the image and starting again is
        // what has to stop the deliveries, because nothing else ever talks to the git host's hook list.
        var gitHost = await ReconcileAsync(judgementImage: null);

        var removed = Assert.Single(gitHost.Removed);
        Assert.Equal("round-1", removed.Organization);
        Assert.Equal(TargetUrl, removed.TargetUrl);
    }
}
