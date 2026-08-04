using Skill.Suite.Application.Abstractions;
using Skill.Suite.Infra.Containers;
using Xunit;

namespace Skill.Suite.Application.Tests.Containers;

/// <summary>
/// Isolation flags that must be on every judgement run, not configurable.
/// </summary>
/// <remarks>
/// A judgement container executes code written by someone with an incentive to score higher, so these are
/// asserted rather than left to a deployment to remember. Resource caps stay configurable — they are a sizing
/// decision — but capabilities and privilege escalation are not.
/// </remarks>
public sealed class ContainerHardeningTests
{
    private static ContainerRunRequest Minimal() => new(
        Image: "judge:1",
        ContainerName: "probe",
        WorkdirVolumeName: "vol",
        WorkdirSubpath: "sub",
        ContainerWorkdir: "/workspace",
        LogMount: null,
        Environment: new Dictionary<string, string>(),
        RegistryAuth: null,
        Limits: new ContainerLimits(IsolateNetwork: true, Memory: null, Cpus: null, PidsLimit: null));

    [Fact]
    public void EveryRunDropsAllCapabilities()
    {
        var args = DockerRunArguments.Build(Minimal());

        var i = args.IndexOf("--cap-drop");
        Assert.True(i >= 0, "a judgement container must not keep Linux capabilities it never needs");
        Assert.Equal("ALL", args[i + 1]);
    }

    [Fact]
    public void EveryRunBlocksPrivilegeEscalation()
    {
        var args = DockerRunArguments.Build(Minimal());

        var i = args.IndexOf("--security-opt");
        Assert.True(i >= 0, "without no-new-privileges a setuid binary undoes the capability drop");
        Assert.Equal("no-new-privileges", args[i + 1]);
    }
}
