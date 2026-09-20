namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// What each placeholder resolves to during the competition, and what changes when marking.
/// </summary>
/// <remarks>
/// The one substantive difference between the two runs lives here, so it is worth stating both ways round:
/// marking swaps the LOGIN and nothing else. Swapping the database name as well would point every marker at
/// the same database, and not swapping the login would bound a marker by the competitor's own grants — on a
/// read-only session, to nothing worth marking.
/// </remarks>
public sealed class ServicePlaceholderValuesTests
{
    private const string BaseName = "round-1";
    private const string Server = "host.docker.internal,1433";

    private static readonly BasicCredential Admin = new("sa", "adm1n-secret");

    private static readonly SessionServicePlanCompetitor Competitor = new(
        Username: "c01",
        FullName: "Joe Doe",
        IpAddress: "10.0.0.7",
        MobileIpAddress: null,
        CountryCode: "HU",
        Password: "correct-horse",
        Ordinal: 2,
        HasDatabaseLogin: true);

    private static Session Draft() =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: "judge:1",
            databaseName: BaseName, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

    private static IReadOnlyDictionary<ServicePlaceholder, string> For(SessionRunMode mode) =>
        ServicePlaceholderValues.For(Draft(), Competitor, mode, BaseName, Server, Admin);

    [Fact]
    public void TheSessionValuesAreTheSessionsOwn()
    {
        var values = For(SessionRunMode.Competition);

        Assert.Equal("Round 1", values[ServicePlaceholder.SessionName]);
        Assert.Equal("round-1", values[ServicePlaceholder.SessionSlug]);
    }

    [Fact]
    public void TheCompetitorValuesAreTheCompetitorsOwn()
    {
        var values = For(SessionRunMode.Competition);

        Assert.Equal("c01", values[ServicePlaceholder.CompetitorUsername]);
        Assert.Equal("Joe Doe", values[ServicePlaceholder.CompetitorFullName]);
        Assert.Equal("10.0.0.7", values[ServicePlaceholder.CompetitorIpAddress]);
        Assert.Equal("HU", values[ServicePlaceholder.CompetitorCountryCode]);
        Assert.Equal("correct-horse", values[ServicePlaceholder.CompetitorPassword]);
    }

    [Fact]
    public void TheDatabaseNameIsTheOneProvisioningCreatedForThisCompetitor()
    {
        Assert.Equal("round-1-c01", For(SessionRunMode.Competition)[ServicePlaceholder.DatabaseName]);
    }

    [Fact]
    public void DuringTheCompetitionTheServiceConnectsAsTheCompetitor()
    {
        var values = For(SessionRunMode.Competition);

        Assert.Equal("c01", values[ServicePlaceholder.DatabaseLogin]);
        Assert.Equal("correct-horse", values[ServicePlaceholder.DatabasePassword]);
    }

    [Fact]
    public void WhileMarkingTheServiceConnectsAsTheDatabaseAdministrator()
    {
        var values = For(SessionRunMode.Marking);

        Assert.Equal("sa", values[ServicePlaceholder.DatabaseLogin]);
        Assert.Equal("adm1n-secret", values[ServicePlaceholder.DatabasePassword]);
    }

    [Fact]
    public void WhileMarkingTheDatabaseIsStillTheCompetitorsOwn()
    {
        // The whole point: the marker connects with full rights TO THAT COMPETITOR'S database, so what they
        // see is that competitor's work rather than a shared one.
        var values = For(SessionRunMode.Marking);

        Assert.Equal("round-1-c01", values[ServicePlaceholder.DatabaseName]);
        Assert.Equal("c01", values[ServicePlaceholder.CompetitorUsername]);
    }

    [Fact]
    public void TheConnectionStringIsBuiltFromTheOtherFourValues()
    {
        var competition = For(SessionRunMode.Competition)[ServicePlaceholder.DatabaseConnectionString];
        var marking = For(SessionRunMode.Marking)[ServicePlaceholder.DatabaseConnectionString];

        Assert.Contains("Database='round-1-c01'", competition, StringComparison.Ordinal);
        Assert.Contains("User ID='c01'", competition, StringComparison.Ordinal);

        Assert.Contains("Database='round-1-c01'", marking, StringComparison.Ordinal);
        Assert.Contains("User ID='sa'", marking, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkingWithNoAdministratorFallsBackToTheCompetitorRatherThanBlanking()
    {
        // Unreachable through the handler, which refuses to start marking without the credential. A blank
        // login here would produce a connection string that fails with an authentication error rather than
        // the missing-credential message the handler already gives.
        var values = ServicePlaceholderValues.For(
            Draft(), Competitor, SessionRunMode.Marking, BaseName, Server, databaseAdmin: null);

        Assert.Equal("c01", values[ServicePlaceholder.DatabaseLogin]);
    }

    [Fact]
    public void ASessionWithNoDatabaseResolvesTheDatabaseValuesToNothing()
    {
        var values = ServicePlaceholderValues.For(
            Draft(), Competitor, SessionRunMode.Competition, databaseBaseName: null, Server, Admin);

        Assert.Equal(string.Empty, values[ServicePlaceholder.DatabaseName]);

        // Null rather than a half-built string is what MsSqlConnectionString returns without a database, and
        // a connection string a competitor would paste and watch fail is worse than none.
        Assert.Equal(string.Empty, values[ServicePlaceholder.DatabaseConnectionString]);
    }

    [Fact]
    public void EveryPlaceholderInTheCatalogueHasAValue()
    {
        var values = For(SessionRunMode.Competition);

        foreach (var placeholder in ServicePlaceholders.All)
            Assert.True(values.ContainsKey(placeholder), ServicePlaceholders.NameOf(placeholder));
    }
}
