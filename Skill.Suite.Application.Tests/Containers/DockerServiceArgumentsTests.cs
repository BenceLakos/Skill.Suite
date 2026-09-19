namespace Skill.Suite.Application.Tests.Containers;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Sessions;
using Skill.Suite.Infra.Containers;
using Xunit;

/// <summary>
/// Asserts the flags a session's long-running service container is actually launched with.
/// </summary>
/// <remarks>
/// A service exists to be reached: a published port, a mount or a restart policy that silently stops being
/// emitted produces a session that looks provisioned and fails the moment a competitor connects. The judgement
/// hardening is asserted absent here for the same reason it is asserted present over there — both directions
/// are decisions, not accidents.
/// </remarks>
public sealed class DockerServiceArgumentsTests
{
    [Fact]
    public void TheServiceIsStartedDetachedUnderItsOwnName()
    {
        var args = DockerServiceArguments.Build(Request());

        Assert.Equal("run", args[0]);

        // Detached: the service outlives the call that started it. Without -d the docker process would hang
        // for the whole session and the caller would never return.
        Assert.Contains("-d", args);

        AssertFlag(args, "--name", "skill-suite-session-7-mssql");
    }

    [Fact]
    public void TheServiceIsRestartedUntilItIsStoppedOnPurpose()
    {
        var args = DockerServiceArguments.Build(Request());

        AssertFlag(args, "--restart", "unless-stopped");
    }

    [Fact]
    public void EveryPortIsPublishedWithItsProtocol()
    {
        var args = DockerServiceArguments.Build(Request());

        var published = ValuesOf(args, "-p");

        Assert.Equal(new[] { "1433:1433/tcp", "5300:53/udp" }, published);
    }

    [Fact]
    public void AReadOnlyVolumeCarriesTheReadOnlySuffix()
    {
        var args = DockerServiceArguments.Build(Request());

        var mounts = ValuesOf(args, "-v");

        Assert.Contains("/srv/session-7/seed:/seed:ro", mounts);
    }

    [Fact]
    public void AWritableVolumeCarriesNoSuffix()
    {
        var args = DockerServiceArguments.Build(Request());

        var mounts = ValuesOf(args, "-v");

        // A stray :ro here would make a database container fail to start with a permissions error that looks
        // like anything but a mount flag.
        Assert.Contains("/srv/session-7/data:/var/opt/mssql", mounts);
    }

    [Fact]
    public void EnvironmentIsPassedThrough()
    {
        var args = DockerServiceArguments.Build(Request());

        AssertFlag(args, "-e", "ACCEPT_EULA=Y");
        Assert.Contains("MSSQL_SA_PASSWORD=s3cret", args);
    }

    [Fact]
    public void LabelsArePassedThrough()
    {
        var args = DockerServiceArguments.Build(Request());

        // The label is how the session's services are found again at close, including after this application
        // has been restarted — losing it strands the containers on the host.
        AssertFlag(args, "--label", "skill-suite.session=7");
    }

    [Fact]
    public void PrivilegeEscalationIsBlocked()
    {
        var args = DockerServiceArguments.Build(Request());

        AssertFlag(args, "--security-opt", "no-new-privileges");
    }

    [Fact]
    public void TheJudgementHardeningIsNotApplied()
    {
        var args = DockerServiceArguments.Build(Request());

        // --network none would make the published ports unreachable, which is the whole point of the service;
        // --cap-drop ALL stops images an administrator chose (a database server, a broker) from starting at
        // all. Neither belongs on infrastructure that is not running competitor code.
        Assert.DoesNotContain("--network", args);
        Assert.DoesNotContain("--cap-drop", args);
    }

    [Fact]
    public void TheImageIsLastAndNoCommandFollowsIt()
    {
        var args = DockerServiceArguments.Build(Request());

        // Nothing may follow the image: the service image's own entrypoint is the service, and an appended
        // command would silently replace it.
        Assert.Equal("registry.example.com/skill09/mssql:2022", args[^1]);
    }

    [Fact]
    public void ARequestWithNothingConfiguredStillProducesARunnableCommand()
    {
        var args = DockerServiceArguments.Build(new ContainerServiceRequest(
            Image: "redis:7",
            ContainerName: "skill-suite-session-7-redis",
            Environment: new Dictionary<string, string>(),
            Labels: new Dictionary<string, string>(),
            Volumes: [],
            PortMappings: [],
            RegistryAuth: null));

        Assert.DoesNotContain("-p", args);
        Assert.DoesNotContain("-v", args);
        Assert.DoesNotContain("-e", args);
        Assert.DoesNotContain("--label", args);
        Assert.Equal("redis:7", args[^1]);
    }

    private static void AssertFlag(List<string> args, string flag, string value)
    {
        var index = args.IndexOf(flag);
        Assert.True(index >= 0, $"expected {flag} in: {string.Join(' ', args)}");
        Assert.Equal(value, args[index + 1]);
    }

    private static List<string> ValuesOf(List<string> args, string flag) =>
        args.Select((a, i) => (a, i)).Where(x => x.a == flag).Select(x => args[x.i + 1]).ToList();

    private static ContainerServiceRequest Request() =>
        new(
            Image: "registry.example.com/skill09/mssql:2022",
            ContainerName: "skill-suite-session-7-mssql",
            Environment: new Dictionary<string, string>
            {
                ["ACCEPT_EULA"] = "Y",
                ["MSSQL_SA_PASSWORD"] = "s3cret",
            },
            Labels: new Dictionary<string, string>
            {
                ["skill-suite.session"] = "7",
            },
            Volumes:
            [
                new VolumeMount("/srv/session-7/data", "/var/opt/mssql", ReadOnly: false),
                new VolumeMount("/srv/session-7/seed", "/seed", ReadOnly: true),
            ],
            PortMappings:
            [
                new PortMapping(1433, 1433, PortProtocol.Tcp),
                new PortMapping(5300, 53, PortProtocol.Udp),
            ],
            RegistryAuth: null);
}
