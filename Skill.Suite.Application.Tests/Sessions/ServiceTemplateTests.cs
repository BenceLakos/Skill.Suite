namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// Reading and filling the <c>{{placeholder}}</c> tokens a service's configuration carries.
/// </summary>
/// <remarks>
/// Everything downstream is decided here. The same scan tells the validators what to refuse and the planner
/// whether a service is one container or twenty, so a token this misses is a service that validates as
/// shared, starts as shared, and hands every competitor the same database.
/// </remarks>
public sealed class ServiceTemplateTests
{
    private static readonly Dictionary<ServicePlaceholder, string> Values = new()
    {
        [ServicePlaceholder.SessionName] = "Round 1",
        [ServicePlaceholder.SessionSlug] = "round-1",
        [ServicePlaceholder.CompetitorUsername] = "c01",
        [ServicePlaceholder.CompetitorFullName] = "Joe Doe",
        [ServicePlaceholder.CompetitorIpAddress] = "10.0.0.1",
        [ServicePlaceholder.CompetitorCountryCode] = "HU",
        [ServicePlaceholder.CompetitorPassword] = "correct-horse",
        [ServicePlaceholder.DatabaseName] = "round-1-c01",
        [ServicePlaceholder.DatabaseServer] = "host.docker.internal,1433",
        [ServicePlaceholder.DatabaseLogin] = "c01",
        [ServicePlaceholder.DatabasePassword] = "correct-horse",
        [ServicePlaceholder.DatabaseConnectionString] = "Server='x';Database='y';",
    };

    [Fact]
    public void EveryPlaceholderInTheCatalogueIsScannedAndRendered()
    {
        // One assertion per placeholder rather than a spot check: a name in the catalogue that the scanner
        // cannot see is a placeholder the form offers and the container never receives.
        foreach (var placeholder in ServicePlaceholders.All)
        {
            var token = ServicePlaceholders.TokenOf(placeholder);

            Assert.Equal([placeholder], ServiceTemplate.Scan([token]).Used);
            Assert.Equal(Values[placeholder], ServiceTemplate.Render(token, Values));
        }
    }

    [Fact]
    public void TextAroundAPlaceholderIsKept()
    {
        Assert.Equal(
            "/srv/round-1/c01/data",
            ServiceTemplate.Render("/srv/{{session.slug}}/{{competitor.username}}/data", Values));
    }

    [Fact]
    public void TheSamePlaceholderTwiceIsRenderedTwiceAndReportedOnce()
    {
        Assert.Equal("c01-c01", ServiceTemplate.Render("{{competitor.username}}-{{competitor.username}}", Values));

        Assert.Equal(
            [ServicePlaceholder.CompetitorUsername],
            ServiceTemplate.Scan(["{{competitor.username}}-{{competitor.username}}"]).Used);
    }

    [Fact]
    public void WhitespaceInsideTheBracesIsIgnored()
    {
        Assert.Equal("c01", ServiceTemplate.Render("{{ competitor.username }}", Values));
    }

    [Fact]
    public void ACaseVariantOfANameStillResolves()
    {
        Assert.Equal("c01", ServiceTemplate.Render("{{Competitor.Username}}", Values));
    }

    [Fact]
    public void AnUncataloguedNameIsReportedRatherThanResolved()
    {
        var scan = ServiceTemplate.Scan(["Server={{database.sever}}"]);

        Assert.Empty(scan.Used);
        Assert.Equal(["database.sever"], scan.Unknown);
    }

    [Fact]
    public void AnUncataloguedNameIsLeftVerbatimWhenRendered()
    {
        // Only reachable for a session saved before the validators existed. Left as written so what reaches
        // the container is what the administrator typed, rather than a silently blanked setting.
        Assert.Equal("Server={{database.sever}}", ServiceTemplate.Render("Server={{database.sever}}", Values));
    }

    [Fact]
    public void TheSameUncataloguedNameIsReportedOnce()
    {
        var scan = ServiceTemplate.Scan(["{{nope}}", "{{NOPE}}"]);

        Assert.Single(scan.Unknown);
    }

    [Fact]
    public void FourBracesAreALiteralPairOfBraces()
    {
        // The only way to configure an image whose own configuration language uses double braces. Without
        // it every one of its braces would read as an unknown placeholder and refuse the save.
        Assert.Equal("{{competitor.username}}", ServiceTemplate.Render("{{{{competitor.username}}", Values));
    }

    [Fact]
    public void AnEscapedPairIsNotScannedAsAPlaceholder()
    {
        var scan = ServiceTemplate.Scan(["{{{{competitor.username}}"]);

        Assert.Empty(scan.Used);
        Assert.Empty(scan.Unknown);
    }

    [Fact]
    public void AnOpenerWithNoCloserIsLeftAlone()
    {
        // Indistinguishable from a value that happens to contain two braces, and there is no name to tell
        // the administrator about.
        Assert.Equal("{{competitor.username", ServiceTemplate.Render("{{competitor.username", Values));
        Assert.Empty(ServiceTemplate.Scan(["{{competitor.username"]).Unknown);
    }

    [Fact]
    public void TextWithNoPlaceholderIsReturnedUnchanged()
    {
        Assert.Equal("postgres", ServiceTemplate.Render("postgres", Values));
    }

    [Fact]
    public void AnEmptyValueRendersEmpty()
    {
        Assert.Equal(string.Empty, ServiceTemplate.Render(null, Values));
        Assert.Equal(string.Empty, ServiceTemplate.Render(string.Empty, Values));
    }

    [Fact]
    public void APlaceholderWithNoValueRendersEmptyRatherThanLeavingTheToken()
    {
        // A literal "{{database.name}}" in a connection string fails as a hostname; an empty one fails as
        // the missing value it is.
        Assert.Equal(
            string.Empty,
            ServiceTemplate.Render("{{database.name}}", new Dictionary<ServicePlaceholder, string>()));
    }

    [Fact]
    public void AServiceIsScannedInItsEnvironmentValuesLabelValuesAndVolumeHostPaths()
    {
        var image = new SessionDockerImage(
            "postgres:17",
            new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" },
            new Dictionary<string, string> { ["owner"] = "{{competitor.fullName}}" },
            [new VolumeMount("/srv/{{session.slug}}", "/data", ReadOnly: false)],
            []);

        var scan = ServiceTemplate.Scan(image);

        Assert.Equal(
            [
                ServicePlaceholder.CompetitorUsername,
                ServicePlaceholder.CompetitorFullName,
                ServicePlaceholder.SessionSlug,
            ],
            scan.Used);
    }

    [Fact]
    public void KeysContainerPathsAndTheImageReferenceAreNotScanned()
    {
        // Keys are the image's contract and the same for everybody; the container path is the image's too;
        // and a per-competitor image reference would defeat the registry listing and the daemon rewrite.
        var image = new SessionDockerImage(
            "{{competitor.username}}/postgres:17",
            new Dictionary<string, string> { ["{{competitor.username}}"] = "fixed" },
            new Dictionary<string, string> { ["{{competitor.username}}"] = "fixed" },
            [new VolumeMount("/srv/data", "/{{competitor.username}}", ReadOnly: false)],
            []);

        var scan = ServiceTemplate.Scan(image);

        Assert.Empty(scan.Used);
        Assert.False(scan.IsPerCompetitor);
    }

    [Fact]
    public void AServiceMentioningNothingStaysShared()
    {
        var scan = ServiceTemplate.Scan(["postgres", "17"]);

        Assert.False(scan.IsPerCompetitor);
        Assert.False(scan.NeedsDatabase);
    }

    [Fact]
    public void AServiceMentioningOnlyTheSessionStaysShared()
    {
        // The session's name and slug have one answer for everybody, so there is nothing to make a second
        // container out of — which is what keeps every session configured before this feature unchanged.
        var scan = ServiceTemplate.Scan(["{{session.slug}}", "{{session.name}}"]);

        Assert.False(scan.IsPerCompetitor);
        Assert.False(scan.NeedsDatabase);
    }

    [Fact]
    public void AServiceMentioningACompetitorBecomesOnePerCompetitor()
    {
        var scan = ServiceTemplate.Scan(["{{competitor.username}}"]);

        Assert.True(scan.IsPerCompetitor);
        Assert.False(scan.NeedsDatabase);
    }

    [Fact]
    public void AServiceMentioningTheDatabaseIsPerCompetitorAndNeedsOne()
    {
        var scan = ServiceTemplate.Scan(["{{database.connectionString}}"]);

        Assert.True(scan.IsPerCompetitor);
        Assert.True(scan.NeedsDatabase);
    }

    [Fact]
    public void AnEmptyScanIsShared()
    {
        Assert.False(ServiceTemplateScan.Empty.IsPerCompetitor);
        Assert.False(ServiceTemplateScan.Empty.NeedsDatabase);
    }
}
