using System.Diagnostics;
using System.Reflection;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// Runs the marker as a real process, with the environment a judge container actually gives it.
/// </summary>
/// <remarks>
/// In-process tests cannot catch the failure this class exists for. The marker runs with
/// <c>LOG_DIRECTORY</c> set, pointing at the same directory as the events file it is appending to — and any
/// code path that touches <c>TestLogger</c> opens that file with <c>FileMode.Create</c> from a static
/// initializer, silently truncating the entire test run before the marker writes a byte. Once the process is
/// the test subject, that whole class of static-initializer side effect is in scope.
/// </remarks>
public sealed class ToolProcessTests
{
    [Fact]
    public void Score_WithLogDirectorySet_PreservesTheTestEvents()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.CopyFixture(Fixture.EventsWithAspects, "events.jsonl");
        var before = File.ReadAllLines(events).Length;

        // The judge container always sets this, and it always points at the events file's own directory.
        var result = RunMarker(workspace, logDirectory: workspace.Path,
            "score",
            "--map", Fixture.Path(Fixture.MapTwoParts),
            "--events", events,
            "--trx", Fixture.Path(Fixture.Trx),
            "--coverage", Fixture.Path(Fixture.Cobertura));

        Assert.Equal(0, result.ExitCode);

        var after = File.ReadAllText(events);

        Assert.DoesNotContain('\0', after);
        Assert.Contains("Validate_WellFormed_Passes", after, StringComparison.Ordinal);
        Assert.True(
            File.ReadAllLines(events).Length > before,
            $"expected the marker to add events; had {before} lines, now {File.ReadAllLines(events).Length}");
        Assert.Contains("\"event\":\"score\"", after, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_WithLogDirectorySet_DoesNotDisturbTheEventsFile()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.CopyFixture(Fixture.EventsWithAspects, "events.jsonl");
        var expected = File.ReadAllText(events);

        var result = RunMarker(workspace, logDirectory: workspace.Path,
            "report",
            "--map", Fixture.Path(Fixture.MapTwoParts),
            "--events", events,
            "--out", workspace.PathTo("cis-report.csv"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, File.ReadAllText(events));
        Assert.Contains("A1.1,yes,2,2", File.ReadAllText(workspace.PathTo("cis-report.csv")), StringComparison.Ordinal);
    }

    [Fact]
    public void UsageError_ExitsTwo()
    {
        using var workspace = new TempWorkspace();

        var result = RunMarker(workspace, logDirectory: null, "score");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--map", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusableMap_ExitsOneAndRecordsAMarkerError()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.CopyFixture(Fixture.EventsWithAspects, "events.jsonl");

        var result = RunMarker(workspace, logDirectory: workspace.Path,
            "score", "--map", workspace.PathTo("absent.json"), "--events", events);

        Assert.Equal(1, result.ExitCode);

        // The diagnostic has to reach the events file, or the run shows a failure with no explanation - and
        // the test events must still be intact.
        var after = File.ReadAllText(events);
        Assert.Contains("\"event\":\"marker-error\"", after, StringComparison.Ordinal);
        Assert.Contains("Validate_WellFormed_Passes", after, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', after);
    }

    private static ProcessResult RunMarker(TempWorkspace workspace, string? logDirectory, params string[] args)
    {
        var directory = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "MarkerDirectory")
            .Value!;

        var dll = Path.Combine(Path.GetFullPath(directory), "Skill.Suite.Marker.dll");
        Assert.True(File.Exists(dll), $"expected the marker at {dll}; build the solution first");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workspace.Path,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(dll);
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        startInfo.Environment["LOG_DIRECTORY"] = logDirectory ?? string.Empty;

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "skill-marker did not exit within 60s");

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
