using System.Globalization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The one definition of the protocol's timestamp format.
/// </summary>
/// <remarks>
/// Previously spelled out three times: in the producer, in the marker's event sink, and in
/// <c>judge-lib.sh</c>. The two C# copies now come from here. The bash copy cannot, so it stays pinned by
/// <c>judge-selftest.sh</c>.
/// </remarks>
public static class Timestamps
{
    /// <summary>The wire format: UTC, millisecond precision, literal trailing <c>Z</c>.</summary>
    /// <remarks>
    /// The <c>Z</c> is a literal rather than a <c>K</c> specifier, matching what has always been emitted.
    /// </remarks>
    public const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>The current instant, formatted for the wire.</summary>
    public static string Now() => DateTime.UtcNow.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>Parses a wire timestamp as a UTC instant.</summary>
    /// <param name="text">The wire value, which may be null, blank or unparseable.</param>
    /// <param name="utc">The parsed instant on success.</param>
    /// <returns>Whether the value could be parsed.</returns>
    public static bool TryParse(string? text, out DateTime utc)
    {
        utc = default;
        return !string.IsNullOrWhiteSpace(text)
            && DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out utc);
    }
}
