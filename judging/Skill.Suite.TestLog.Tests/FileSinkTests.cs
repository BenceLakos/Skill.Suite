using System.Diagnostics;
using System.Reflection;
using Skill.Suite.TestLog.Protocol;
using Skill.Suite.TestLog.Tests.Support;
using Xunit;

namespace Skill.Suite.TestLog.Tests;

/// <summary>
/// Exercises the file sink out of process, which is the only way it can be tested.
/// </summary>
/// <remarks>
/// <c>TestLogger</c>'s sink is a <c>static readonly</c> field resolved on first touch of the type, so
/// <c>LOG_DIRECTORY</c> is read exactly once per process. Rather than weaken the shipped artifact with
/// a mutable-sink test seam, these tests run the quickstart sample as a child process — which also
/// makes them an end-to-end check of the documented "new console app produces results" claim.
/// </remarks>
public sealed class FileSinkTests
{
    private const string EventFileName = "events.jsonl";

    [Fact]
    public void WithLogDirectory_WritesEventsFileAndKeepsStdoutClean()
    {
        using var workspace = new TempDirectory();

        var result = RunQuickstart(workspace.Path);

        Assert.Equal(0, result.ExitCode);

        var eventFile = Path.Combine(workspace.Path, EventFileName);
        Assert.True(File.Exists(eventFile), $"expected {eventFile} to exist");

        var lines = EventNormalizer.NormalizeAll(File.ReadAllLines(eventFile));
        AssertQuickstartStream(lines);

        // Events must not be duplicated onto stdout: the platform parses the file when it exists and
        // the stdout copy would only inflate the captured container log.
        Assert.DoesNotContain("start-fixture", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutLogDirectory_WritesTheSameStreamToStdout()
    {
        using var workspace = new TempDirectory();

        var result = RunQuickstart(logDirectory: null);

        Assert.Equal(0, result.ExitCode);
        AssertQuickstartStream(EventNormalizer.NormalizeAll(result.Stdout.Split('\n')));
        Assert.False(File.Exists(Path.Combine(workspace.Path, EventFileName)));
    }

    [Fact]
    public void WithLogDirectory_TruncatesPerRunRatherThanAppending()
    {
        using var workspace = new TempDirectory();

        RunQuickstart(workspace.Path);
        var first = File.ReadAllLines(Path.Combine(workspace.Path, EventFileName)).Length;
        RunQuickstart(workspace.Path);
        var second = File.ReadAllLines(Path.Combine(workspace.Path, EventFileName)).Length;

        // FileMode.Create: each run starts fresh. This is why any other writer sharing the file must
        // append *and* run after the test host has exited.
        Assert.Equal(first, second);
    }

    [Fact]
    public void WithLogDirectory_CreatesTheDirectoryWhenMissing()
    {
        using var workspace = new TempDirectory();
        var nested = Path.Combine(workspace.Path, "nested", "logs");

        var result = RunQuickstart(nested);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(nested, EventFileName)));
    }

    [Fact]
    public void WithUnusableLogDirectory_FallsBackToStdoutInsteadOfFailing()
    {
        // A path that cannot be created: /dev/null is a file, so nothing can be made beneath it.
        var result = RunQuickstart("/dev/null/not-a-directory");

        Assert.Equal(0, result.ExitCode);
        AssertQuickstartStream(EventNormalizer.NormalizeAll(result.Stdout.Split('\n')));
    }

    private static void AssertQuickstartStream(string[] lines)
    {
        var events = lines.Where(line => line.StartsWith('{')).ToArray();

        Assert.Equal(
            """{"event":"start-fixture","fixture":"QuickstartChecks","timestamp":"<ts>"}""",
            events[0]);

        Assert.Single(events, line => line.Contains("\"event\":\"finish-fixture\"", StringComparison.Ordinal));
        Assert.Equal(2, events.Count(line => line.Contains("\"event\":\"start-unit-test\"", StringComparison.Ordinal)));
        Assert.Equal(2, events.Count(line => line.Contains("\"event\":\"finish-unit-test\"", StringComparison.Ordinal)));

        // One passes, one fails - the sample shows both paths.
        Assert.Single(events, line => line.Contains("\"outcome\":\"passed\"", StringComparison.Ordinal));
        Assert.Single(events, line => line.Contains("\"outcome\":\"failed\"", StringComparison.Ordinal));

        // The tallies live on the test-summary; the score reports only its verdict.
        var summary = Assert.IsType<TestSummaryEvent>(
            TestLogEventReader.Read(events.Single(line => line.Contains(TestLogEventTypes.Of<TestSummaryEvent>(), StringComparison.Ordinal))));
        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Passed);

        var score = Assert.IsType<ScoreEvent>(TestLogEventReader.Read(events[^1]));
        Assert.Equal(MetricEvent.OverallPart, score.PartOrOverall);
        Assert.Equal(0.5, score.Value);
    }

    private static ProcessResult RunQuickstart(string? logDirectory)
    {
        var directory = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "QuickstartDirectory")
            .Value!;

        var dll = Path.Combine(Path.GetFullPath(directory), "ConsoleQuickstart.dll");
        Assert.True(File.Exists(dll), $"expected the quickstart sample at {dll}; build the solution first");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "exec", dll },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Explicitly clear rather than leave inherited: the parent test process cleared it too, but a
        // developer running with LOG_DIRECTORY exported must not silently change what is asserted.
        startInfo.Environment["LOG_DIRECTORY"] = logDirectory ?? string.Empty;

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "quickstart sample did not exit within 60s");

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

    private sealed class TempDirectory : IDisposable
    {
        internal string Path { get; } =
            Directory.CreateTempSubdirectory("skill-suite-testlog-").FullName;

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
        }
    }
}
