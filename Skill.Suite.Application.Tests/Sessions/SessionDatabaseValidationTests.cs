namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions;
using Skill.Suite.Application.Sessions.CreateSession;
using Skill.Suite.Application.Sessions.UpdateSession;
using Xunit;

/// <summary>
/// What a session may say about its competitors' databases: the base name, and the seed script.
/// </summary>
/// <remarks>
/// Asserted against both validators rather than one, because the rules are worth as much on an edit as on a
/// create: a session edited into a state it cannot be started from is discovered at exactly the moment the
/// competition is supposed to begin. The path rules are the store's own — repeated here only so the
/// administrator is told while they are still looking at the form.
/// </remarks>
public sealed class SessionDatabaseValidationTests
{
    private const string ValidScript = "fibonacci-session/seed/init.sql";
    private const string DatabaseName = "skill09";

    [Theory]
    [InlineData(ValidScript)]
    [InlineData("a-package/INIT.SQL")]
    [InlineData("a-package/db/01 create tables.sql")]
    public void AScriptFromThePackagesWithADatabaseToRunItAgainstIsAccepted(string script)
    {
        Assert.True(Create(script, DatabaseName).IsValid);
        Assert.True(Update(script, DatabaseName).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoScriptIsFineWithOrWithoutADatabase(string? script)
    {
        Assert.True(Create(script, null).IsValid);
        Assert.True(Update(script, DatabaseName).IsValid);
    }

    [Fact]
    public void AScriptWithNoDatabaseNameIsRejected()
    {
        // There would be nothing to run it against, and the session would refuse to start — which is the
        // wrong moment to find out.
        Assert.False(Create(ValidScript, null).IsValid);
        Assert.False(Update(ValidScript, "   ").IsValid);
    }

    [Theory]
    [InlineData("a-package/seed/init.txt")]
    [InlineData("a-package/seed/init.sql.bak")]
    [InlineData("a-package/seed")]
    public void SomethingThatIsNotASqlFileIsRejected(string script)
    {
        Assert.False(Create(script, DatabaseName).IsValid);
        Assert.False(Update(script, DatabaseName).IsValid);
    }

    [Theory]
    [InlineData("../etc/init.sql")]
    [InlineData("a-package/../../etc/init.sql")]
    [InlineData("..\\etc\\init.sql")]
    [InlineData("/etc/init.sql")]
    [InlineData("./init.sql")]
    public void APathThatLeavesTheStarterPackagesIsRejected(string script)
    {
        Assert.False(Create(script, DatabaseName).IsValid);
        Assert.False(Update(script, DatabaseName).IsValid);
    }

    [Fact]
    public void APathLongerThanTheColumnIsRejected()
    {
        var script = "a-package/" + new string('x', 1000) + ".sql";

        Assert.False(Create(script, DatabaseName).IsValid);
        Assert.False(Update(script, DatabaseName).IsValid);
    }

    [Fact]
    public void ABaseNameThatLeavesRoomForAUsernameIsAccepted()
    {
        var baseName = new string('b', SessionDatabaseNaming.MaxBaseNameLength);

        Assert.True(Create(null, baseName).IsValid);
        Assert.True(Update(null, baseName).IsValid);
    }

    [Fact]
    public void ABaseNameThatWouldOverflowTheIdentifierIsRejected()
    {
        // Not a database name on its own: a base at the old 120-character limit plus a separator plus a
        // username is past the 128 SQL Server allows, and every competitor's database would fail to be
        // created for a reason the form could have said first.
        var baseName = new string('b', SessionDatabaseNaming.MaxBaseNameLength + 1);

        Assert.False(Create(null, baseName).IsValid);
        Assert.False(Update(null, baseName).IsValid);
    }

    private static FluentValidation.Results.ValidationResult Create(string? script, string? databaseName) =>
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
            DatabaseSeedScript: script,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: null,
            DockerImages: []));

    private static FluentValidation.Results.ValidationResult Update(string? script, string? databaseName) =>
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
            DatabaseSeedScript: script,
            GitCredentialId: Guid.NewGuid(),
            JudgementImagePullCredentialId: null,
            DockerImages: []));
}
