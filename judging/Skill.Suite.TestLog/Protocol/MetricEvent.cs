using System.Text.Json.Serialization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// An aggregate measurement for a scoring part, rather than a single test's lifecycle.
/// </summary>
/// <param name="Part">
/// Scoring part. Omitted from the wire when blank, which readers interpret as the reserved <c>overall</c>
/// rollup.
/// </param>
/// <param name="Value">
/// The event's headline ratio, in 0..1.
/// </param>
/// <remarks>
/// <para>
/// <b>Every metric event has exactly one <see cref="Value"/>, and the event kind fixes what it means.</b> That
/// is the whole point of the consolidation: a pass rate, a line-coverage ratio, a mutation kill rate and a
/// composite quality are all "the number this event exists to report", so they share one name instead of four.
/// </para>
/// <para>
/// <b>The formula is not uniform, and the contract does not pretend otherwise.</b> Coverage divides by
/// <c>total</c>; mutation divides by <c>covered</c>, because a mutant no test reached says nothing about the
/// suite. Each derived record documents its own formula.
/// </para>
/// <para>
/// The platform consumes exactly one of these values — <see cref="ScoreEvent"/>'s, which becomes the
/// competitor-visible quality. The rest are stored opaquely and displayed. Having them share a named position
/// in the type system is what makes that single functional read obvious.
/// </para>
/// </remarks>
public abstract record MetricEvent(string? Part, double? Value) : TestLogEvent
{
    /// <summary>The reserved part name for the whole-submission rollup.</summary>
    public const string OverallPart = "overall";

    /// <summary>The part this event scores, defaulting to <see cref="OverallPart"/> when unset.</summary>
    [JsonIgnore]
    public string PartOrOverall => string.IsNullOrWhiteSpace(Part) ? OverallPart : Part!;
}
