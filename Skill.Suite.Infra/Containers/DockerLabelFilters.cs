namespace Skill.Suite.Infra.Containers;

/// <summary>
/// Builds the <c>docker ps</c> filter arguments that narrow a listing to containers carrying every label.
/// </summary>
/// <remarks>
/// Split out from <see cref="ProcessContainerServiceManager"/> so it can be asserted directly, the same way
/// <see cref="DockerServiceArguments"/> is — and for a sharper reason: this is what decides which containers
/// get force-removed. A filter that silently stopped being emitted would not fail; it would widen the match
/// and take down containers the caller never meant to touch.
/// <para>
/// A repeated <c>--filter label=k=v</c> is an AND in <c>docker ps</c>, which is exactly the narrowing
/// removing one competitor's marking containers needs: the marking label says which session, the competitor
/// label says whose, and either alone is too wide.
/// </para>
/// </remarks>
internal static class DockerLabelFilters
{
    private const string Filter = "--filter";
    private const string LabelPrefix = "label=";
    private const char KeyValueSeparator = '=';

    /// <summary>The full <c>docker ps</c> argument list for listing container ids by label.</summary>
    internal static List<string> ListArguments(IReadOnlyDictionary<string, string> labels)
    {
        List<string> args = ["ps", "-a", "-q"];

        foreach (var expression in Expressions(labels))
        {
            args.Add(Filter);
            args.Add(expression);
        }

        return args;
    }

    /// <summary>The filter expressions on their own, in key order, for logs and error messages.</summary>
    internal static IEnumerable<string> Expressions(IReadOnlyDictionary<string, string> labels) =>
        labels
            .OrderBy(label => label.Key, StringComparer.Ordinal)
            .Select(label => $"{LabelPrefix}{label.Key}{KeyValueSeparator}{label.Value}");
}
