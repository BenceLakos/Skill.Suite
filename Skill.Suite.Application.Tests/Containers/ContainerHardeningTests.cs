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
    public void EveryRunAddsBackExactlyTheCapabilitiesTheJudgeNeeds()
    {
        var args = DockerRunArguments.Build(Minimal());

        var added = args
            .Select((value, index) => (value, index))
            .Where(pair => pair.value == "--cap-add")
            .Select(pair => args[pair.index + 1])
            .ToList();

        // Pinned as a whole list rather than one containment check each, because both directions have already
        // cost a competition. Too few: dropping ALL without SETUID killed setpriv and the run produced nothing,
        // and dropping KILL left root unable to signal the unprivileged test step, so an endless loop was
        // recorded as Completed. Too many: every entry here is a capability a judgement container holds while
        // executing code written to score higher.
        Assert.Equal(
            new[] { "SETUID", "SETGID", "CHOWN", "DAC_OVERRIDE", "FOWNER", "KILL" },
            added);
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
