namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// The names stopping a session has to reach, worked out without resolving anything secret.
/// </summary>
/// <remarks>
/// Stopping derives its names exactly as the planner derives the ones it started the containers under, which
/// is what stops a container being started under one name and looked for under another — the failure that
/// leaves a container running, a host port held, and a competitor still able to reach a service after the
/// session was suspended.
/// </remarks>
public sealed class SessionServiceContainersTests
{
    private static Session SessionWith(params SessionDockerImage[] images) =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: "judge:1",
            databaseName: "round-1", databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: images).Value;

    private static SessionDockerImage Image(string image, Dictionary<string, string>? env = null) =>
        new(image, env ?? [], [], [], [], Domain: null);

    [Fact]
    public void EveryServiceIsOneNamePerCompetitor()
    {
        var containers = SessionServiceContainers.For(
            SessionWith(Image("redis:8")), ["c01", "c02"], SessionRunMode.Competition);

        Assert.Equal(
            ["skill-suite-round-1-1-c01", "skill-suite-round-1-1-c02"],
            containers.Select(c => c.ContainerName));
    }

    [Fact]
    public void ADatabaseServiceStillNamesEveryCompetitorEvenThoughStartingSkippedSome()
    {
        // Stopping a container that was never created is a no-op the daemon reports as absent; missing one
        // leaves it running. Reconstructing every name is the cheaper mistake by a distance.
        var containers = SessionServiceContainers.For(
            SessionWith(Image("postgres:17", new Dictionary<string, string>
            {
                ["CONNECTION"] = "{{database.connectionString}}",
            })),
            ["c01", "c02", "c03"],
            SessionRunMode.Competition);

        Assert.Equal(3, containers.Count);
    }

    [Fact]
    public void TheNamesAreTheOnesThePlannerStartedTheContainersUnder()
    {
        var session = SessionWith(
            Image("postgres:17", new Dictionary<string, string> { ["USER"] = "{{competitor.username}}" }),
            Image("redis:8"));

        var planned = SessionServicePlanner.Plan(new SessionServicePlanRequest(
            session,
            SessionRunMode.Competition,
            SessionProvisioningStage.DockerServices,
            [new SessionServicePlanCompetitor("c01", "Joe", "10.0.0.1", "HU", "p", 0, true)],
            "round-1",
            "host.docker.internal,1433",
            DatabaseAdmin: null,
            GitInternalBaseUrl: null,
            "skill-suite",
            MarkingIpAddress: null));

        var stopped = SessionServiceContainers.For(session, ["c01"], SessionRunMode.Competition);

        Assert.Equal(
            planned.Services.Select(s => s.ContainerName).Order(),
            stopped.Select(c => c.ContainerName).Order());
    }

    [Fact]
    public void MarkingNamesAreTheMarkingOnes()
    {
        var containers = SessionServiceContainers.For(
            SessionWith(Image("redis:8")), ["c01"], SessionRunMode.Marking);

        Assert.Equal("skill-suite-round-1-1-c01-marking", Assert.Single(containers).ContainerName);
    }

    [Fact]
    public void AFailureSubjectNamesTheCompetitorAsWellAsTheImage()
    {
        var containers = SessionServiceContainers.For(
            SessionWith(Image("postgres:17")), ["c01"], SessionRunMode.Competition);

        Assert.Equal("postgres:17 (c01)", Assert.Single(containers).Subject);
    }
}
