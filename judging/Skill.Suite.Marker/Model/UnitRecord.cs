namespace Skill.Suite.Marker.Model;

/// <summary>
/// One unit test as replayed from the event stream.
/// </summary>
/// <param name="Fixture">Owning fixture name.</param>
/// <param name="Test">Test case name.</param>
/// <param name="Aspect">Declared marking aspect, or <see langword="null"/> when ungraded.</param>
/// <param name="Outcome">Final verdict after applying the promotion rules.</param>
public sealed record UnitRecord(string Fixture, string Test, string? Aspect, UnitOutcome Outcome);
