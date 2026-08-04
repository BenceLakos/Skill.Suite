using Skill.Suite.Application.Abstractions;
using Skill.Suite.Infra.Containers;
using Xunit;

namespace Skill.Suite.Application.Tests.Containers;

/// <summary>
/// Asserts the flags a judgement container is actually launched with.
/// </summary>
/// <remarks>
/// These are the boundary between a competitor's test code and the host. A dropped flag leaves a system that
/// behaves perfectly until someone abuses it, which is exactly the kind of regression that never shows up in
/// a functional test.
/// </remarks>
public sealed class DockerRunArgumentsTests
{
    [Fact]
    public void SourceCheckoutIsMountedReadOnlyAtTheWorkingDirectory()
    {
        var args = DockerRunArguments.Build(Request());

        var mount = args[args.IndexOf("--mount") + 1];
        Assert.Contains("volume-subpath=submission-alice-abc", mount, StringComparison.Ordinal);

        // readonly matters: the checkout is the competitor's own code and the judge must not be able to
        // rewrite it, and volume-subpath is what stops one submission seeing its siblings on the same volume.
        Assert.EndsWith(",readonly", mount, StringComparison.Ordinal);

        Assert.Equal("/workspace", args[args.IndexOf("-w") + 1]);
    }

    [Fact]
    public void NetworkIsolationIsAppliedByDefault()
    {
        // ContainerLimits defaults IsolateNetwork to true. If this ever flips, a competitor's test code gets
        // outbound internet access - enough to exfiltrate the hidden suite it is being judged against.
        var args = DockerRunArguments.Build(Request(new ContainerLimits()));

        AssertFlag(args, "--network", "none");
    }

    [Fact]
    public void NetworkIsolationCanBeDisabledExplicitly()
    {
        var args = DockerRunArguments.Build(Request(new ContainerLimits(IsolateNetwork: false)));

        Assert.DoesNotContain("--network", args);
    }

    [Fact]
    public void ResourceCapsArePassedWhenConfigured()
    {
        var args = DockerRunArguments.Build(
            Request(new ContainerLimits(Memory: "2g", Cpus: "1.5", PidsLimit: 512)));

        AssertFlag(args, "--memory", "2g");
        AssertFlag(args, "--cpus", "1.5");
        AssertFlag(args, "--pids-limit", "512");
    }

    [Fact]
    public void ResourceCapsAreOmittedWhenUnset()
    {
        // Unset by default on purpose: a black-box session runs mutation testing, and an arbitrary memory cap
        // would kill it in a way that looks like a competitor's bug.
        var args = DockerRunArguments.Build(Request(new ContainerLimits()));

        Assert.DoesNotContain("--memory", args);
        Assert.DoesNotContain("--cpus", args);
        Assert.DoesNotContain("--pids-limit", args);
    }

    [Fact]
    public void PidsLimitIsFormattedInvariantly()
    {
        // The machine's culture is not necessarily invariant; a localized number here would be rejected by
        // the docker CLI.
        var args = DockerRunArguments.Build(Request(new ContainerLimits(PidsLimit: 1024)));

        AssertFlag(args, "--pids-limit", "1024");
    }

    [Fact]
    public void WithoutLimits_NoIsolationFlagsAreEmitted()
    {
        var args = DockerRunArguments.Build(Request(limits: null));

        Assert.DoesNotContain("--network", args);
        Assert.DoesNotContain("--memory", args);
    }

    [Fact]
    public void TheLogMountIsWritable()
    {
        var args = DockerRunArguments.Build(Request());

        var mounts = args.Select((a, i) => (a, i)).Where(x => x.a == "--mount").Select(x => args[x.i + 1]).ToList();
        var logMount = Assert.Single(mounts, m => m.Contains("/var/log/skill-suite", StringComparison.Ordinal));

        // No `readonly`: the judge writes events.jsonl here.
        Assert.DoesNotContain("readonly", logMount, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentIsPassedThrough()
    {
        var args = DockerRunArguments.Build(Request());

        AssertFlag(args, "-e", "COMPETITOR_DIRECTORY=/workspace");
        Assert.Contains("LOG_DIRECTORY=/var/log/skill-suite", args);
    }

    [Fact]
    public void TheImageIsLastAndNoCommandFollowsIt()
    {
        var args = DockerRunArguments.Build(Request(new ContainerLimits(Memory: "1g")));

        // Nothing may follow the image: the judgement image's own ENTRYPOINT is the judge, and an appended
        // command would silently replace it.
        Assert.Equal("judge:1", args[^1]);
    }

    private static void AssertFlag(List<string> args, string flag, string value)
    {
        var index = args.IndexOf(flag);
        Assert.True(index >= 0, $"expected {flag} in: {string.Join(' ', args)}");
        Assert.Equal(value, args[index + 1]);
    }

    private static ContainerRunRequest Request(ContainerLimits? limits = null) =>
        new(
            Image: "judge:1",
            ContainerName: "testrun-abc",
            WorkdirVolumeName: "skill-suite-workdir",
            WorkdirSubpath: "submission-alice-abc",
            ContainerWorkdir: "/workspace",
            Environment: new Dictionary<string, string>
            {
                ["COMPETITOR_DIRECTORY"] = "/workspace",
                ["LOG_DIRECTORY"] = "/var/log/skill-suite",
            },
            LogMount: new ContainerLogMount("skill-suite-workdir", "submission-alice-abc.log", "/var/log/skill-suite"),
            Limits: limits);
}
