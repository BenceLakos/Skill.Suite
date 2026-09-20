using Skill.Suite.Marker.Model;
using Skill.Suite.TestLog.Protocol;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Replays an <c>events.jsonl</c> stream into per-unit verdicts.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately mirrors the platform consumer rather than trusting <c>finish-unit-test</c>: a <c>call</c>
/// carrying <c>threw</c> promotes the unit to <see cref="UnitOutcome.Errored"/> and an <c>assertion</c>
/// that did not pass promotes it to <see cref="UnitOutcome.Failed"/>, and neither can be downgraded by a later
/// outcome claiming success. If this diverged, the CIS report and the UI would disagree about the same
/// submission — and a suite could pass an aspect by emitting an optimistic final outcome.
/// </para>
/// <para>
/// Reading is <see cref="TestLogEventReader"/>'s job. This class used to carry its own JSON probing, its own
/// outcome mapping and its own field-name literals — a second, independently drifting parser for the same
/// stream. What is left here is only the replay logic, which is genuinely marker-specific.
/// </para>
/// </remarks>
public static class EventReplay
{
    /// <summary>Reads the stream and returns one record per unit test, in first-seen order.</summary>
    /// <param name="path">Path to the events file.</param>
    /// <returns>The replayed units.</returns>
    /// <exception cref="FileNotFoundException">The events file does not exist.</exception>
    public static IReadOnlyList<UnitRecord> Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"events file not found: {path}", path);

        var units = new Dictionary<(string Fixture, string Test), Builder>();
        var order = new List<(string Fixture, string Test)>();

        // ReadFile drops corrupt lines, matching the platform's tolerance: one bad line in a competitor's log
        // must not discard the rest of their run.
        foreach (var @event in TestLogEventReader.ReadFile(path))
            Apply(@event, units, order);

        return [.. order.Select(key => units[key].Build())];
    }

    /// <summary>Names every fixture the stream mentions.</summary>
    /// <param name="path">Path to the events file. An absent file yields nothing rather than failing.</param>
    /// <returns>Distinct fixture names, in no particular order.</returns>
    /// <remarks>
    /// Read from the harness's own <c>start-fixture</c> and per-test events, so the names are exactly the
    /// strings a fixture-scoped metric has to match. Absent-is-empty rather than absent-is-an-error, because
    /// the marker's own event file may not exist yet on a run that produced no tests at all.
    /// </remarks>
    public static IReadOnlyCollection<string> ReadFixtureNames(string path)
    {
        var fixtures = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(path)) return fixtures;

        foreach (var @event in TestLogEventReader.ReadFile(path))
        {
            var name = @event switch
            {
                StartFixtureEvent e => e.Fixture,
                FinishFixtureEvent e => e.Fixture,
                _ => Identify(@event)?.Fixture,
            };

            if (!string.IsNullOrWhiteSpace(name)) fixtures.Add(name);
        }

        return fixtures;
    }

    private static void Apply(
        TestLogEvent @event,
        Dictionary<(string Fixture, string Test), Builder> units,
        List<(string Fixture, string Test)> order)
    {
        // Metric and diagnostic events carry no unit identity.
        var identity = Identify(@event);
        if (identity is not var (fixture, test)) return;
        if (fixture is null || test is null) return;

        var key = (fixture, test);

        if (@event is StartUnitTestEvent && !units.ContainsKey(key))
        {
            units[key] = new Builder(fixture, test);
            order.Add(key);
        }

        // Like the platform, per-test events for a unit that never started are dropped.
        if (!units.TryGetValue(key, out var builder)) return;

        switch (@event)
        {
            case StartUnitTestEvent e:
                builder.SetAspect(e.Aspect);
                break;

            case CallEvent { Threw: { Length: > 0 } }:
                builder.Promote(UnitOutcome.Errored);
                break;

            case AssertionEvent { Passed: false }:
                builder.Promote(UnitOutcome.Failed);
                break;

            case FinishUnitTestEvent e:
                builder.SetAspect(e.Aspect);
                builder.Finish(MapOutcome(e.Outcome));
                break;
        }
    }

    /// <summary>The fixture and test an event belongs to, or null for events with no unit identity.</summary>
    private static (string? Fixture, string? Test)? Identify(TestLogEvent @event) => @event switch
    {
        StartUnitTestEvent e => (e.Fixture, e.Test),
        FinishUnitTestEvent e => (e.Fixture, e.Test),
        CallEvent e => (e.Fixture, e.Test),
        AssertionEvent e => (e.Fixture, e.Test),
        _ => null,
    };

    private static UnitOutcome MapOutcome(TestLogOutcome outcome) => outcome switch
    {
        TestLogOutcome.Passed => UnitOutcome.Passed,
        TestLogOutcome.Failed => UnitOutcome.Failed,
        TestLogOutcome.Skipped => UnitOutcome.Skipped,
        TestLogOutcome.Errored => UnitOutcome.Errored,
        _ => UnitOutcome.Unknown,
    };

    private sealed class Builder(string fixture, string test)
    {
        private string? _aspect;
        private UnitOutcome _outcome = UnitOutcome.Unknown;
        private bool _broken;

        internal void SetAspect(string? aspect)
        {
            if (!string.IsNullOrWhiteSpace(aspect)) _aspect = aspect;
        }

        internal void Promote(UnitOutcome outcome)
        {
            _outcome = outcome;
            _broken = true;
        }

        internal void Finish(UnitOutcome outcome)
        {
            // A broken verdict stands: the final outcome may not talk its way back to passed.
            if (_broken) return;
            _outcome = outcome;
        }

        internal UnitRecord Build() => new(fixture, test, _aspect, _outcome);
    }
}
