using Skill.Suite.Marker.Map;

namespace Skill.Suite.Marker.Model;

/// <summary>
/// Routes a test class name or source path to the scoring part that owns it.
/// </summary>
/// <param name="parts">Declared parts, already normalized.</param>
public sealed class PartRouter(IReadOnlyList<PartRule> parts)
{
    private static readonly char[] Separators = ['.', '/', '\\'];

    /// <summary>
    /// Returns the part owning <paramref name="qualifiedName"/>, or <see langword="null"/> when no part
    /// claims it.
    /// </summary>
    /// <param name="qualifiedName">
    /// A namespace-qualified type name (<c>Acme.Validator.Tests.FooTests</c>) or a file path
    /// (<c>src/Validator/Foo.cs</c>, or an absolute Windows path).
    /// </param>
    /// <remarks>
    /// Matches whole segments only. A part named <c>validator</c> therefore claims a
    /// <c>Validator</c> namespace or folder but not a type merely containing the word, which is what
    /// substring matching got wrong. Unrouted items still count towards <c>overall</c>.
    /// </remarks>
    public string? Route(string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName) || parts.Count == 0) return null;

        var segments = qualifiedName.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            foreach (var segment in segments)
            {
                if (part.Segments.Contains(segment, StringComparer.OrdinalIgnoreCase))
                    return part.Id;
            }
        }

        return null;
    }
}
