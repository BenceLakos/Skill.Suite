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
        List<VolumeMount>? volumes = null,
        List<PortMapping>? ports = null,
        string? domain = null,
        int? routedPort = null) =>
        new("postgres:17", env ?? [], labels ?? [], volumes ?? [], ports ?? [], domain, routedPort);

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

    private static SessionDockerImage Routed(string domain, int? routedPort = 8080, List<PortMapping>? ports = null) =>
        Image(ports: ports, domain: domain, routedPort: routedPort);

    [Theory]
    [InlineData("shop.skills.local")]
    [InlineData("shop")]
    [InlineData("a-b.c-d.example")]
    [InlineData("s1.skills.local")]
    public void ABareLowercaseHostnameWithAContainerPortIsAccepted(string domain)
    {
        Assert.True(Create([Routed(domain)], DatabaseName).IsValid);
        Assert.True(Update([Routed(domain)], DatabaseName).IsValid);
    }

    [Fact]
    public void ARoutedServiceNeedsNoPortMappingAtAll()
    {
        // The proxy reaches the container over the shared docker network and talks to the container port
        // directly, so nothing has to be published on the host.
        Assert.True(Create([Routed("shop.skills.local", ports: [])], DatabaseName).IsValid);
    }

    [Fact]
    public void ARoutedServiceMayStillPublishHostPorts()
    {
        // Port mappings stay optional and independent of routing, in both directions.
        Assert.True(Create(
            [Routed("shop.skills.local", ports: [new PortMapping(8080, 80, PortProtocol.Tcp)])],
            DatabaseName).IsValid);
    }

    [Fact]
    public void ARoutedServiceWhoseOnlyPortMappingIsUdpIsStillAccepted()
    {
        // The old rule demanded a TCP mapping. Nothing about routing needs one.
        Assert.True(Create(
            [Routed("shop.skills.local", ports: [new PortMapping(5300, 53, PortProtocol.Udp)])],
            DatabaseName).IsValid);
    }

    [Fact]
    public void NoDomainIsFineAndNeedsNoPorts()
    {
        Assert.True(Create([Image()], DatabaseName).IsValid);
    }

    [Fact]
    public void ADomainWithNoContainerPortIsRejected()
    {
        // There would be nothing for the proxy to forward to: the route is created and every request
        // through it fails.
        Assert.False(Create([Routed("shop.skills.local", routedPort: null)], DatabaseName).IsValid);
        Assert.False(Update([Routed("shop.skills.local", routedPort: null)], DatabaseName).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void AContainerPortOutsideThePortRangeIsRejected(int routedPort)
    {
        Assert.False(Create([Routed("shop.skills.local", routedPort)], DatabaseName).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void AContainerPortAtTheEdgesOfTheRangeIsAccepted(int routedPort)
    {
        Assert.True(Create([Routed("shop.skills.local", routedPort)], DatabaseName).IsValid);
    }

    [Fact]
    public void AContainerPortWithoutADomainIsRejected()
    {
        // Refused rather than ignored: a port left behind by clearing the domain would read as a service
        // that is still routed, which it is not.
        Assert.False(Create([Image(routedPort: 8080)], DatabaseName).IsValid);
        Assert.False(Update([Image(routedPort: 8080)], DatabaseName).IsValid);
    }

    [Theory]
    [InlineData("Shop.Skills.Local")]
    [InlineData("SHOP")]
    public void AnUppercaseDomainIsRejected(string domain)
    {
        // The rule is a literal in a generated label, and two spellings of one name is what an administrator
        // would be left comparing when a route does not match.
        Assert.False(Create([Routed(domain)], DatabaseName).IsValid);
        Assert.False(Update([Routed(domain)], DatabaseName).IsValid);
    }

    [Theory]
    [InlineData("http://shop.skills.local")]
    [InlineData("https://shop.skills.local")]
    public void ADomainWithASchemeIsRejected(string domain)
    {
        Assert.False(Create([Routed(domain)], DatabaseName).IsValid);
    }

    [Fact]
    public void ADomainWithAPortIsRejected()
    {
        // The proxy matches the Host header, and the port the container listens on is a field of its own.
        Assert.False(Create([Routed("shop.skills.local:8080")], DatabaseName).IsValid);
    }

    [Theory]
    [InlineData("shop.skills.local/admin")]
    [InlineData("shop..local")]
    [InlineData("-shop.local")]
    [InlineData("shop.local-")]
    [InlineData("shop local")]
    public void ADomainThatIsNotAHostnameIsRejected(string domain)
    {
        Assert.False(Create([Routed(domain)], DatabaseName).IsValid);
    }

    [Fact]
    public void ADomainLongerThanADnsNameIsRejected()
    {
        var domain = string.Join('.', Enumerable.Repeat(new string('a', 60), 5));

        Assert.True(domain.Length > ServiceDomain.MaxLength);
        Assert.False(Create([Routed(domain)], DatabaseName).IsValid);
    }

    [Fact]
    public void TwoServicesSharingADomainAreRejected()
    {
        // Two routers competing for the same requests, resolved by a tie-break nothing here controls.
        var images = new List<SessionDockerImage>
        {
            Routed("shop.skills.local"),
            Routed("SHOP.skills.local".ToLowerInvariant()),
        };

        Assert.False(Create(images, DatabaseName).IsValid);
        Assert.False(Update(images, DatabaseName).IsValid);
    }

    [Fact]
    public void TwoServicesWithDifferentDomainsAreAccepted()
    {
        var images = new List<SessionDockerImage>
        {
            Routed("shop.skills.local"),
            Routed("admin.skills.local", 3000),
        };

        Assert.True(Create(images, DatabaseName).IsValid);
    }

    [Fact]
    public void TwoServicesWithoutDomainsAreAccepted()
    {
        Assert.True(Create([Image(), Image()], DatabaseName).IsValid);
    }

    [Fact]
    public void TheExistingImageRulesStillApply()
    {
        var blank = new SessionDockerImage("   ", [], [], [], [], Domain: null, RoutedPort: null);

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
