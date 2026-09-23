namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions;
using Skill.Suite.Application.Sessions.CreateSession;
using Skill.Suite.Application.Sessions.UpdateSession;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// A session with no judgement image: what it is allowed to do, and what stops happening for it.
/// </summary>
/// <remarks>
/// Not every session is judged. Some exist only to put the git organisation, the competitor repositories,
/// the per-competitor databases and the service containers in place, and are marked by an expert afterwards.
/// Start used to refuse such a session outright, which made the whole shape unreachable — the administrator
/// was left inventing a judgement image that would never be pulled just to get the repositories created.
/// <para>
/// <see cref="Session.RequiresJudgement"/> is the single predicate the rest of the system branches on:
/// <c>StartSessionHandler</c> installs the organisation webhook only when it holds and removes the hook when
/// it does not, and <c>ProcessGitWebhookHandler</c> refuses to open a run when it does not. Asserted here
/// rather than through those handlers because both of them are a provisioning pipeline over a git host, a
/// SQL Server and a docker daemon; the decision itself is this one line, and it is worth pinning on its own.
/// </para>
/// </remarks>
public sealed class OptionalJudgementImageTests
{
    private static Session Draft(string? judgementImage) =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: judgementImage,
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ASessionWithNoJudgementImageStarts(string? judgementImage)
    {
        var session = Draft(judgementImage);

        var started = session.Start([1, 2, 3]);

        Assert.True(started.IsSuccess);
        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public void StartingWithoutAJudgementImageStillStampsTheWebhookSecret()
    {
        // Stamped even though no hook is installed for it, deliberately: adding an image later and starting
        // again then installs the hook with the key the session has always had, instead of needing a second
        // transition to produce one.
        var session = Draft(judgementImage: null);

        session.Start([9, 8, 7]);

        Assert.Equal([9, 8, 7], session.WebhookSecret);
    }

    [Fact]
    public void StartStillDemandsATemplateFolder()
    {
        // The image going optional must not take the other Start guards with it: without a starter package
        // every competitor is handed an empty repository.
        var session = Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
            templateFolder: null, judgementImage: null,
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

        var started = session.Start([1]);

        Assert.True(started.IsFailure);
        Assert.Equal(SessionErrors.MissingTemplateFolder, started.Error);
        Assert.Equal(SessionStatus.Draft, session.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ASessionWithNoJudgementImageIsNotJudged(string? judgementImage)
    {
        // Whitespace counts as absent because that is what the form produces from a cleared autocomplete,
        // and Create normalises it to null anyway — the predicate must not disagree with either.
        Assert.False(Draft(judgementImage).RequiresJudgement);
    }

    [Fact]
    public void ASessionNamingAJudgementImageIsJudged()
    {
        Assert.True(Draft("judge:1").RequiresJudgement);
    }

    [Fact]
    public void ClearingTheImageOnAnUpdateTurnsJudgementOff()
    {
        // The route an administrator actually takes: a session that was judged, edited to stop being. Start
        // is then what reconciles the git host, removing the hook it installed the first time.
        var session = Draft("judge:1");

        session.UpdateDetails(
            "Round 1", null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: null,
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []);

        Assert.False(session.RequiresJudgement);
        Assert.Null(session.JudgementImage);
    }

    [Fact]
    public void TheDtoCarriesWhetherTheSessionIsJudged()
    {
        // The admin pages decide what to show from the DTO alone. Re-deriving it there would be a second
        // place for "blank means not judged" to be spelled differently.
        Assert.False(SessionMapper.ToDto(Draft(null)).RequiresJudgement);
        Assert.True(SessionMapper.ToDto(Draft("judge:1")).RequiresJudgement);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoJudgementImageIsAcceptedBySaving(string? judgementImage)
    {
        Assert.True(Create(judgementImage, pullCredentialId: null).IsValid);
        Assert.True(Update(judgementImage, pullCredentialId: null).IsValid);
    }

    [Fact]
    public void APullCredentialWithNoJudgementImageIsAccepted()
    {
        // Ignored rather than refused: the same credential is what the session's own service images are
        // pulled with, so a session with no judgement image and a private service registry needs it.
        Assert.True(Create(null, Guid.NewGuid()).IsValid);
        Assert.True(Update(null, Guid.NewGuid()).IsValid);
    }

    private static FluentValidation.Results.ValidationResult Create(
        string? judgementImage, Guid? pullCredentialId) =>
        new CreateSessionValidator().Validate(new CreateSessionCommand(
            "Round 1",
            "round-1",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            TemplateFolder: "/starter-packages/a-package/competitor-start",
            JudgementImage: judgementImage,
            DatabaseName: null,
            DatabaseReadAccess: true,
            DatabaseWriteAccess: true,
            DatabaseSeedScript: null,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: pullCredentialId,
            DockerImages: []));

    private static FluentValidation.Results.ValidationResult Update(
        string? judgementImage, Guid? pullCredentialId) =>
        new UpdateSessionValidator().Validate(new UpdateSessionCommand(
            Guid.NewGuid(),
            "Round 1",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            TemplateFolder: "/starter-packages/a-package/competitor-start",
            JudgementImage: judgementImage,
            DatabaseName: null,
            DatabaseReadAccess: true,
            DatabaseWriteAccess: true,
            DatabaseSeedScript: null,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: pullCredentialId,
            DockerImages: []));
}
