namespace Skill.Suite.Marker.Map;

/// <summary>
/// A <c>marking-map.json</c> that cannot be used: missing, malformed, or self-contradictory.
/// </summary>
/// <remarks>
/// Treated as a processing error rather than a usage error — the command line was well-formed, so the
/// run emits <c>marker-error</c> and exits 1 rather than printing usage and exiting 2.
/// </remarks>
public sealed class MarkingMapException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public MarkingMapException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and the underlying cause.</summary>
    public MarkingMapException(string message, Exception inner) : base(message, inner) { }
}
