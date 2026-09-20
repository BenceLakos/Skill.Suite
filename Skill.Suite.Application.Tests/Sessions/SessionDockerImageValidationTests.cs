namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.CreateSession;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Application.Sessions.UpdateSession;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// What a session's docker services may say, checked while the administrator is still looking at the form.
/// </summary>
/// <remarks>
/// Asserted against both validators rather than one, for the reason the database rules are: a session edited
/// into a state its services cannot be started from is discovered at exactly the moment the competition is
/// supposed to begin.
/// <para>
/// An unknown placeholder is the mistake worth refusing a save over. It is not resolved and not blanked — it
/// reaches the container verbatim — so a connection string carrying <c>{{database.sever}}</c> starts a
/// perfectly healthy container that fails every query, hours after the form could have said so.
/// </para>
/// </remarks>
public sealed class SessionDockerImageValidationTests
{
    private const string DatabaseName = "round-1";

    private static SessionDockerImage Image(
        Dictionary<string, string>? env = null,
        Dictionary<string, string>? labels = null,
        List<VolumeMount>? volumes = null) =>
        new("postgres:17", env ?? [], labels ?? [], volumes ?? [], []);

    private static SessionDockerImage WithEnv(string value) =>
        Image(env: new Dictionary<string, string> { ["SETTING"] = value });

    [Fact]
    public void AServiceUsingNoPlaceholdersIsAccepted()
    {
        Assert.True(Create([Image()], DatabaseName).IsValid);
        Assert.True(Update([Image()], DatabaseName).IsValid);
    }

    [Fact]
    public void EveryPlaceholderInTheCatalogueIsAccepted()
    {
        // The form lists them, so every one of them has to save. A name offered and then refused would be
        // worse than not offering it.
        var images = ServicePlaceholders.All.Select(p => WithEnv(ServicePlaceholders.TokenOf(p))).ToList();

        Assert.True(Create(images, DatabaseName).IsValid);
        Assert.True(Update(images, DatabaseName).IsValid);
    }

    [Fact]
    public void AnUnknownPlaceholderIsRejected()
    {
        Assert.False(Create([WithEnv("Server={{database.sever}}")], DatabaseName).IsValid);
        Assert.False(Update([WithEnv("Server={{database.sever}}")], DatabaseName).IsValid);
    }

    [Fact]
    public void TheMessageNamesTheOffendingPlaceholder()
    {
        var result = Create([WithEnv("{{competitor.nickname}}")], DatabaseName);

        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("{{competitor.nickname}}", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownPlaceholderInALabelValueIsRejected()
    {
        var image = Image(labels: new Dictionary<string, string> { ["owner"] = "{{competitor.nickname}}" });

        Assert.False(Create([image], DatabaseName).IsValid);
    }

    [Fact]
    public void AnUnknownPlaceholderInAVolumeHostPathIsRejected()
    {
        var image = Image(volumes: [new VolumeMount("/srv/{{competitor.nickname}}", "/data", false)]);

        Assert.False(Create([image], DatabaseName).IsValid);
    }

    [Fact]
    public void AnEscapedBracePairIsNotReadAsAPlaceholder()
    {
        // An image whose own configuration language uses double braces has to be configurable at all.
        Assert.True(Create([WithEnv("{{{{ .Values.name }}")], DatabaseName).IsValid);
    }

    [Fact]
    public void ADatabasePlaceholderWithoutADatabaseBaseNameIsRejected()
    {
        // The service would be started for nobody: no competitor has a session database to point it at.
        Assert.False(Create([WithEnv("{{database.connectionString}}")], databaseName: null).IsValid);
        Assert.False(Update([WithEnv("{{database.name}}")], databaseName: "   ").IsValid);
    }

    [Fact]
    public void ACompetitorPlaceholderNeedsNoDatabase()
    {
        Assert.True(Create([WithEnv("{{competitor.username}}")], databaseName: null).IsValid);
        Assert.True(Update([WithEnv("{{competitor.username}}")], databaseName: null).IsValid);
    }

    [Fact]
    public void TheExistingImageRulesStillApply()
    {
        var blank = new SessionDockerImage("   ", [], [], [], []);

        Assert.False(Create([blank], DatabaseName).IsValid);
        Assert.False(Update([blank], DatabaseName).IsValid);
    }

    private static FluentValidation.Results.ValidationResult Create(
        List<SessionDockerImage> images, string? databaseName) =>
        new CreateSessionValidator().Validate(new CreateSessionCommand(
            "Round 1",
            "round-1",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            TemplateFolder: "/starter-packages/a-package/competitor-start",
            JudgementImage: "judge:1",
            DatabaseName: databaseName,
            DatabaseReadAccess: true,
            DatabaseWriteAccess: true,
            DatabaseSeedScript: null,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: null,
            DockerImages: images));

    private static FluentValidation.Results.ValidationResult Update(
        List<SessionDockerImage> images, string? databaseName) =>
        new UpdateSessionValidator().Validate(new UpdateSessionCommand(
            Guid.NewGuid(),
            "Round 1",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            TemplateFolder: "/starter-packages/a-package/competitor-start",
            JudgementImage: "judge:1",
            DatabaseName: databaseName,
            DatabaseReadAccess: true,
            DatabaseWriteAccess: true,
            DatabaseSeedScript: null,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: null,
            DockerImages: images));
}
