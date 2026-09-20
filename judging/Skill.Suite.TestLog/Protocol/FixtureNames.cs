namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The one definition of how a test class becomes a fixture name.
/// </summary>
/// <remarks>
/// <para>
/// The producer names a fixture <c>typeof(TFixture).Name</c> — the class's <b>simple</b> name, no namespace.
/// Anything that later wants to attach a measurement to that fixture has to arrive at the same string from a
/// namespace-qualified name instead: a TRX <c>className</c>, or a mutation report's test name. That conversion
/// lives here rather than in each consumer, because a consumer that derives it slightly differently does not
/// fail — it silently produces a second fixture nobody asked for, next to the real one.
/// </para>
/// <para>
/// Two names that differ only by namespace collapse onto one fixture. That is not a defect of this type but of
/// the convention it serves: the producer already names fixtures without their namespace, so two same-named
/// test classes in different namespaces were always one fixture in the stream.
/// </para>
/// </remarks>
public static class FixtureNames
{
    private const char NamespaceSeparator = '.';
    private const char NestedTypeSeparator = '+';
    private const char ArgumentListStart = '(';

    /// <summary>The fixture name for a namespace-qualified type name, such as a TRX <c>className</c>.</summary>
    /// <param name="qualifiedTypeName">For example <c>Acme.Widgets.WidgetTests</c>.</param>
    /// <returns>The simple class name, or <see langword="null"/> when there is nothing usable.</returns>
    public static string? FromTypeName(string? qualifiedTypeName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedTypeName)) return null;

        var name = qualifiedTypeName.Trim();

        // A TRX className is occasionally assembly-qualified ("Ns.Type, Assembly, Version=...").
        var comma = name.IndexOf(',');
        if (comma >= 0) name = name[..comma].TrimEnd();

        var start = name.LastIndexOfAny([NamespaceSeparator, NestedTypeSeparator]) + 1;
        var simple = name[start..].Trim();

        return simple.Length == 0 ? null : simple;
    }

    /// <summary>The fixture name for a fully-qualified test name, such as a mutation report's test name.</summary>
    /// <param name="qualifiedTestName">
    /// For example <c>Acme.Widgets.WidgetTests.Add_Works</c>, or a display name carrying its theory arguments:
    /// <c>Acme.Widgets.WidgetTests.Add(left: 1, right: 2)</c>.
    /// </param>
    /// <returns>The declaring class's simple name, or <see langword="null"/> when the name carries no class.</returns>
    /// <remarks>
    /// The argument list is removed <b>before</b> anything else, because a theory's arguments routinely contain
    /// dots of their own and every split after that point would land inside them.
    /// </remarks>
    public static string? FromTestName(string? qualifiedTestName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedTestName)) return null;

        var name = qualifiedTestName.Trim();

        var arguments = name.IndexOf(ArgumentListStart);
        if (arguments >= 0) name = name[..arguments].TrimEnd();

        var lastSeparator = name.LastIndexOfAny([NamespaceSeparator, NestedTypeSeparator]);

        // A bare method name has no class in it. Inventing one would be worse than admitting that.
        return lastSeparator < 0 ? null : FromTypeName(name[..lastSeparator]);
    }
}
