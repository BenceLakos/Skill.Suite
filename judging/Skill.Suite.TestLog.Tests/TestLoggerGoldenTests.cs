using Skill.Suite.TestLog.Tests.Support;
using Xunit;

namespace Skill.Suite.TestLog.Tests;

/// <summary>
/// Compares a full scripted run against the checked-in golden stream.
/// </summary>
/// <remarks>
/// The same golden file is replayed through the platform's real <c>TestLogParser</c> by
/// <c>Skill.Suite.Application.Tests</c>, so this pins producer and consumer to one artifact. On
/// mismatch the actual output is written next to the golden as <c>.actual</c> for diffing; review it
/// before promoting, since a golden is only as good as the review that accepted it.
/// </remarks>
public sealed class TestLoggerGoldenTests : EventFixture
{
    private const string GoldenName = "protocol-v1.jsonl";

    [Fact]
    public void ScriptedRun_MatchesGoldenStream()
    {
        ScriptedRun.Emit();
        var actual = NormalizedLines();

        if (!File.Exists(GoldenFile.PathTo(GoldenName)))
        {
            var written = GoldenFile.WriteActual(GoldenName, actual);
            Assert.Fail($"Golden file is missing. Review and promote: {written}");
        }

        var expected = GoldenFile.Read(GoldenName);

        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            var written = GoldenFile.WriteActual(GoldenName, actual);
            Assert.Fail(
                $"Emitted stream does not match {GoldenName}. Actual written to {written}." +
                Environment.NewLine + Diff(expected, actual));
        }
    }

    [Fact]
    public void ScriptedRun_EmitsEveryEventKindExactlyOnceWhereExpected()
    {
        ScriptedRun.Emit();
        var lines = NormalizedLines();

        Assert.Single(lines, line => line.Contains("\"event\":\"start-fixture\"", StringComparison.Ordinal));
        Assert.Single(lines, line => line.Contains("\"event\":\"finish-fixture\"", StringComparison.Ordinal));
        Assert.Equal(4, lines.Count(line => line.Contains("\"event\":\"start-unit-test\"", StringComparison.Ordinal)));
        Assert.Equal(4, lines.Count(line => line.Contains("\"event\":\"finish-unit-test\"", StringComparison.Ordinal)));
        Assert.Equal(3, lines.Count(line => line.Contains("\"event\":\"assertion\"", StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(line => line.Contains("\"event\":\"call\"", StringComparison.Ordinal)));
        Assert.Single(lines, line => line.Contains("\"event\":\"test-summary\"", StringComparison.Ordinal));
        Assert.Single(lines, line => line.Contains("\"event\":\"score\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ScriptedRun_StartsEveryUnitBeforeItsOtherEvents()
    {
        ScriptedRun.Emit();
        var lines = NormalizedLines();

        // The consumer drops call/assertion/finish events for a test it never saw start, so ordering
        // here is a correctness property of the producer, not a cosmetic one.
        foreach (var test in new[]
                 {
                     ScriptedRun.PassingTest, ScriptedRun.FailingTest,
                     ScriptedRun.ErroredTest, ScriptedRun.UnannotatedTest,
                 })
        {
            var indexed = lines
                .Select((line, index) => (line, index))
                .Where(x => x.line.Contains($"\"test\":\"{test}\"", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(indexed);
            Assert.Contains("\"event\":\"start-unit-test\"", indexed[0].line, StringComparison.Ordinal);
            Assert.Contains("\"event\":\"finish-unit-test\"", indexed[^1].line, StringComparison.Ordinal);
        }
    }

    private static string Diff(string[] expected, string[] actual)
    {
        var lines = new List<string>();
        for (var i = 0; i < Math.Max(expected.Length, actual.Length); i++)
        {
            var e = i < expected.Length ? expected[i] : "<missing>";
            var a = i < actual.Length ? actual[i] : "<missing>";
            if (!string.Equals(e, a, StringComparison.Ordinal))
                lines.Add($"line {i + 1}:{Environment.NewLine}  expected: {e}{Environment.NewLine}  actual:   {a}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
