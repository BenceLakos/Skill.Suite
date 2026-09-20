namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// How many containers a session's images become, what they are called, and what is in them.
/// </summary>
/// <remarks>
/// The rule this suite exists for: a service whose environment values, label values or volume host paths
/// mention any competitor- or database-scoped placeholder is started once per competitor, except that one
/// mentioning the database is started only for competitors the SQL Server holds a login for; anything else
/// stays the single shared container sessions have always had.
/// </remarks>
public sealed class SessionServicePlannerTests
{
    private const string BaseName = "round-1";
    private const string Server = "host.docker.internal,1433";

    private static readonly BasicCredential Admin = new("sa", "adm1n");

    private static SessionServicePlanCompetitor Competitor(
        string username, int ordinal, bool hasLogin = true) =>
        new(username, $"{username} Doe", "10.0.0.1", "HU", $"{username}-pass", ordinal, hasLogin);

    private static Session SessionWith(params SessionDockerImage[] images) =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: "judge:1",
            databaseName: BaseName, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: images).Value;

    private static SessionDockerImage Image(
        string image = "postgres:17",
        Dictionary<string, string>? env = null,
        Dictionary<string, string>? labels = null,
        List<VolumeMount>? volumes = null,
        List<PortMapping>? ports = null) =>
        new(image, env ?? [], labels ?? [], volumes ?? [], ports ?? []);

    private static SessionServicePlan Plan(
        Session session,
        SessionRunMode mode = SessionRunMode.Competition,
        params SessionServicePlanCompetitor[] competitors) =>
        SessionServicePlanner.Plan(new SessionServicePlanRequest(
            session,
            mode,
            mode == SessionRunMode.Marking
                ? SessionProvisioningStage.MarkingServices
                : SessionProvisioningStage.DockerServices,
            competitors,
            BaseName,
            Server,
            Admin,
            GitInternalBaseUrl: null));

    [Fact]
    public void AServiceWithNoPlaceholdersIsOneSharedContainerWhateverTheCompetitorCount()
    {
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string> { ["MODE"] = "fixed" })),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1));

        var service = Assert.Single(plan.Services);

        // Exactly the name sessions produced before placeholders existed, so an upgrade does not start a
        // second copy of a service that is already running.
        Assert.Equal("skill-suite-round-1-1", service.ContainerName);
        Assert.Null(service.CompetitorUsername);
    }

    [Fact]
    public void ASharedServiceStillResolvesTheSessionPlaceholders()
    {
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string> { ["NAME"] = "{{session.name}}" })),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        Assert.Equal("Round 1", Assert.Single(plan.Services).Environment["NAME"]);
    }

    [Fact]
    public void AServiceNamingACompetitorIsOneContainerPerCompetitor()
    {
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" })),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1));

        Assert.Equal(2, plan.Services.Count);
        Assert.Equal("skill-suite-round-1-1-c01", plan.Services[0].ContainerName);
        Assert.Equal("skill-suite-round-1-1-c02", plan.Services[1].ContainerName);
        Assert.Equal("c01", plan.Services[0].Environment["USER"]);
        Assert.Equal("c02", plan.Services[1].Environment["USER"]);
    }

    [Fact]
    public void AVolumeHostPathIsSubstitutedAndTheContainerPathIsNot()
    {
        var plan = Plan(
            SessionWith(Image(volumes: [new VolumeMount("/srv/{{competitor.username}}", "/data", true)])),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        var volume = Assert.Single(Assert.Single(plan.Services).Volumes);

        Assert.Equal("/srv/c01", volume.HostPath);
        Assert.Equal("/data", volume.ContainerPath);
        Assert.True(volume.ReadOnly);
    }

    [Fact]
    public void ALabelValueIsSubstitutedAndThePlatformsOwnLabelsAreAdded()
    {
        var plan = Plan(
            SessionWith(Image(labels: new Dictionary<string, string> { ["owner"] = "{{competitor.fullName}}" })),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        var labels = Assert.Single(plan.Services).Labels;

        Assert.Equal("c01 Doe", labels["owner"]);
        Assert.Equal("round-1", labels[SessionServiceLabels.SessionKey]);
        Assert.Equal("1", labels[SessionServiceLabels.ServiceKey]);
        Assert.Equal("c01", labels[SessionServiceLabels.CompetitorKey]);
        Assert.False(labels.ContainsKey(SessionServiceLabels.MarkingKey));
    }

    [Fact]
    public void ASharedContainerCarriesNoCompetitorLabel()
    {
        var plan = Plan(SessionWith(Image()), SessionRunMode.Competition, Competitor("c01", 0));

        Assert.False(Assert.Single(plan.Services).Labels.ContainsKey(SessionServiceLabels.CompetitorKey));
    }

    [Fact]
    public void ThePlatformsLabelsWinOverOnesTheImageCarriesItself()
    {
        // They are what Close and Stop Marking find the containers by; a session label an image set to its
        // own value would strand its container on the host.
        var plan = Plan(
            SessionWith(Image(labels: new Dictionary<string, string>
            {
                [SessionServiceLabels.SessionKey] = "something-else",
            })),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        Assert.Equal("round-1", Assert.Single(plan.Services).Labels[SessionServiceLabels.SessionKey]);
    }

    [Fact]
    public void EachCompetitorsCopyPublishesItsOwnHostPorts()
    {
        var plan = Plan(
            SessionWith(Image(
                env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" },
                ports: [new PortMapping(5432, 5432, PortProtocol.Tcp)])),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1), Competitor("c03", 2));

        Assert.Equal([5432, 5433, 5434], plan.Services.Select(s => s.PortMappings[0].HostPort));
        Assert.All(plan.Services, service => Assert.Equal(5432, service.PortMappings[0].ContainerPort));
    }

    [Fact]
    public void ASharedServiceKeepsThePortsExactlyAsConfigured()
    {
        var plan = Plan(
            SessionWith(Image(ports: [new PortMapping(5432, 5432, PortProtocol.Tcp)])),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1));

        Assert.Equal(5432, Assert.Single(plan.Services).PortMappings[0].HostPort);
    }

    [Fact]
    public void ACompetitorWhosePortWouldLeaveTheRangeIsRecordedAndTheRestAreStillPlanned()
    {
        var plan = Plan(
            SessionWith(Image(
                env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" },
                ports: [new PortMapping(ServicePortAllocation.MaxPort, 80, PortProtocol.Tcp)])),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1));

        Assert.Single(plan.Services);
        Assert.Equal("c01", plan.Services[0].CompetitorUsername);

        var failure = Assert.Single(plan.Failures);
        Assert.Equal("c02", failure.Subject);
        Assert.Equal(SessionProvisioningStage.DockerServices, failure.Stage);
    }

    [Fact]
    public void ADatabaseServiceIsStartedOnlyForCompetitorsWhoHoldASqlLogin()
    {
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string>
            {
                ["CONNECTION"] = "{{database.connectionString}}",
            })),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1, hasLogin: false));

        Assert.Equal("c01", Assert.Single(plan.Services).CompetitorUsername);

        // Skipped, not failed: nothing was attempted, because no database of theirs was ever created.
        Assert.Equal(["c02"], plan.SkippedNoDatabaseLogin);
        Assert.Empty(plan.Failures);
    }

    [Fact]
    public void ACompetitorServiceThatNeedsNoDatabaseIsStartedForEverybody()
    {
        // A SQL login has nothing to do with a service that only wants their username.
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" })),
            SessionRunMode.Competition,
            Competitor("c01", 0), Competitor("c02", 1, hasLogin: false));

        Assert.Equal(2, plan.Services.Count);
        Assert.Empty(plan.SkippedNoDatabaseLogin);
    }

    [Fact]
    public void ACompetitorSkippedByTwoServicesIsReportedOnce()
    {
        var plan = Plan(
            SessionWith(
                Image(env: new Dictionary<string, string> { ["A"] = "{{database.name}}" }),
                Image(env: new Dictionary<string, string> { ["B"] = "{{database.server}}" })),
            SessionRunMode.Competition,
            Competitor("c01", 0, hasLogin: false));

        Assert.Equal(["c01"], plan.SkippedNoDatabaseLogin);
    }

    [Fact]
    public void MarkingSuffixesEveryContainerAndLabelsItWithTheSessionSlug()
    {
        var plan = Plan(
            SessionWith(
                Image(env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" }),
                Image("redis:8")),
            SessionRunMode.Marking,
            Competitor("c01", 0));

        Assert.Equal("skill-suite-round-1-1-c01-marking", plan.Services[0].ContainerName);

        // The shared service gets a marking copy too, so the marking setup is complete on its own.
        Assert.Equal("skill-suite-round-1-2-marking", plan.Services[1].ContainerName);

        Assert.All(plan.Services, service =>
        {
            Assert.Equal("round-1", service.Labels[SessionServiceLabels.MarkingKey]);
            Assert.Equal("round-1", service.Labels[SessionServiceLabels.SessionKey]);
        });
    }

    [Fact]
    public void MarkingConnectsAsTheAdministratorToTheSameCompetitorDatabase()
    {
        var plan = Plan(
            SessionWith(Image(env: new Dictionary<string, string>
            {
                ["LOGIN"] = "{{database.login}}",
                ["PASSWORD"] = "{{database.password}}",
                ["DATABASE"] = "{{database.name}}",
                ["USER"] = "{{competitor.username}}",
            })),
            SessionRunMode.Marking,
            Competitor("c01", 0));

        var environment = Assert.Single(plan.Services).Environment;

        Assert.Equal("sa", environment["LOGIN"]);
        Assert.Equal("adm1n", environment["PASSWORD"]);
        Assert.Equal("round-1-c01", environment["DATABASE"]);
        Assert.Equal("c01", environment["USER"]);
    }

    [Fact]
    public void MarkingReusesTheSamePerCompetitorPorts()
    {
        // The competition containers were removed at close, so there is nothing to collide with — and an
        // expert who wrote a port down against a competitor finds the same one.
        var image = Image(
            env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" },
            ports: [new PortMapping(5432, 5432, PortProtocol.Tcp)]);

        var competition = Plan(SessionWith(image), SessionRunMode.Competition, Competitor("c02", 1));
        var marking = Plan(SessionWith(image), SessionRunMode.Marking, Competitor("c02", 1));

        Assert.Equal(
            competition.Services[0].PortMappings[0].HostPort,
            marking.Services[0].PortMappings[0].HostPort);
    }

    [Fact]
    public void TwoServicesOfOneSessionAreNumberedFromOne()
    {
        var plan = Plan(
            SessionWith(Image("postgres:17"), Image("redis:8")),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        Assert.Equal(["1", "2"], plan.Services.Select(s => s.ServiceNumber));
    }

    [Fact]
    public void ASessionWithNoCompetitorsPlansOnlyItsSharedServices()
    {
        var plan = Plan(
            SessionWith(
                Image("redis:8"),
                Image("postgres:17", env: new Dictionary<string, string> { ["U"] = "{{competitor.username}}" })),
            SessionRunMode.Competition);

        Assert.Equal("redis:8", Assert.Single(plan.Services).ConfiguredImage);
    }

    [Fact]
    public void TheStoredImageReferenceIsKeptAlongsideTheOneTheDaemonIsHanded()
    {
        var plan = SessionServicePlanner.Plan(new SessionServicePlanRequest(
            SessionWith(Image("gitea:3000/skill09/postgres:17")),
            SessionRunMode.Competition,
            SessionProvisioningStage.DockerServices,
            [],
            BaseName,
            Server,
            Admin,
            GitInternalBaseUrl: "http://gitea:3000"));

        var service = Assert.Single(plan.Services);

        Assert.Equal("gitea:3000/skill09/postgres:17", service.ConfiguredImage);
        Assert.Equal("localhost:3000/skill09/postgres:17", service.Image);
    }

    [Fact]
    public void AFailureSubjectNamesTheCompetitorAsWellAsTheImage()
    {
        // "postgres:17 failed" leaves the admin to work out which of twenty competitors is without a service.
        var plan = Plan(
            SessionWith(Image(
                env: new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" },
                ports: [new PortMapping(8080, 80, PortProtocol.Tcp)])),
            SessionRunMode.Competition,
            Competitor("c01", 0));

        Assert.Equal("postgres:17 (c01)", Assert.Single(plan.Services).Subject);
    }
}
