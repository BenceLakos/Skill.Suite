namespace Skill.Suite.Application.Sessions.Services;

using System.Text;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Reads and fills the <c>{{placeholder}}</c> tokens a session's docker service configuration may carry.
/// </summary>
/// <remarks>
/// Pure and shared by everything that has an opinion about a template: the validators scan to refuse a name
/// nobody resolves, the planner scans to decide whether the service is one container or one per competitor,
/// and both start handlers render with it. A second implementation anywhere would be a service that
/// validates as shared and starts per competitor, or the reverse.
/// <para>
/// Scanned and rendered by hand rather than with a regular expression, because the escape has to be part of
/// the same left-to-right pass: a pattern that matches tokens cannot also know it is sitting inside an
/// escaped brace pair.
/// </para>
/// </remarks>
internal static class ServiceTemplate
{
    /// <summary>Opens a placeholder.</summary>
    internal const string Open = "{{";

    /// <summary>Closes a placeholder.</summary>
    internal const string Close = "}}";

    /// <summary>
    /// Written where a literal <see cref="Open"/> is wanted, and rendered back down to one.
    /// </summary>
    /// <remarks>
    /// The only way to get a literal <c>{{</c> past the scanner. Without it a service image whose own
    /// configuration language uses double braces could not be configured at all, because every one of its
    /// braces would be read as an unknown placeholder and refuse the save.
    /// </remarks>
    internal const string EscapedOpen = "{{{{";

    /// <summary>
    /// The fields of a service that are templated: environment values, label values and volume host paths.
    /// </summary>
    /// <remarks>
    /// Values, not keys. A key is the name the image reads the setting under — part of the image's contract,
    /// the same for every competitor — while the value is what the session author has to vary. The same
    /// reasoning leaves the volume's CONTAINER path out and the host path in: the path inside the container
    /// is the image's, the path on the competition machine is the administrator's, and a per-competitor data
    /// folder is the whole reason volumes are here.
    /// <para>
    /// The image reference is deliberately not templated. It is resolved against a registry, rewritten for
    /// the host daemon by <see cref="Skill.Suite.Application.DockerImages.DaemonImageReference"/> and offered
    /// from a registry listing on the form; a per-competitor reference would defeat all three and pull N
    /// images to run the same service.
    /// </para>
    /// </remarks>
    public static IEnumerable<string> FieldsOf(SessionDockerImage image) =>
        image.Env.Values
            .Concat(image.Labels.Values)
            .Concat(image.Volumes.Select(volume => volume.HostPath));

    /// <summary>What a whole service's configuration asks for, and what it asks for that does not exist.</summary>
    public static ServiceTemplateScan Scan(SessionDockerImage image) => Scan(FieldsOf(image));

    public static ServiceTemplateScan Scan(IEnumerable<string?> texts)
    {
        var used = new List<ServicePlaceholder>();
        var unknown = new List<string>();

        foreach (var text in texts)
        {
            foreach (var name in Tokens(text))
            {
                if (ServicePlaceholders.TryParse(name, out var placeholder))
                {
                    if (!used.Contains(placeholder))
                        used.Add(placeholder);
                }
                else if (!unknown.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    unknown.Add(name);
                }
            }
        }

        return new ServiceTemplateScan(used, unknown);
    }

    /// <summary>
    /// <paramref name="text"/> with every known placeholder replaced by its value.
    /// </summary>
    /// <remarks>
    /// A placeholder with no value in <paramref name="values"/> renders as empty rather than being left in
    /// place: the callers only ever render a template they have already scoped values for, and leaving the
    /// token behind would put the literal string <c>{{database.name}}</c> into a connection string, where it
    /// fails as a hostname instead of as an obviously missing value.
    /// </remarks>
    public static string Render(string? text, IReadOnlyDictionary<ServicePlaceholder, string> values)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Nothing to substitute and nothing to unescape is the common case — a plain value typed by an
        // administrator who never used a placeholder — and it must not allocate a builder.
        if (!text.Contains(Open, StringComparison.Ordinal))
            return text;

        var rendered = new StringBuilder(text.Length);
        var position = 0;

        while (position < text.Length)
        {
            var open = text.IndexOf(Open, position, StringComparison.Ordinal);
            if (open < 0)
                break;

            rendered.Append(text, position, open - position);

            if (IsEscaped(text, open))
            {
                rendered.Append(Open);
                position = open + EscapedOpen.Length;
                continue;
            }

            var close = text.IndexOf(Close, open + Open.Length, StringComparison.Ordinal);
            if (close < 0)
                break;

            var name = text[(open + Open.Length)..close].Trim();

            if (ServicePlaceholders.TryParse(name, out var placeholder))
                rendered.Append(values.TryGetValue(placeholder, out var value) ? value : string.Empty);
            else
                rendered.Append(text, open, close + Close.Length - open);

            position = close + Close.Length;
        }

        return rendered.Append(text, position, text.Length - position).ToString();
    }

    /// <summary>
    /// The placeholder names <paramref name="text"/> mentions, in the order they appear.
    /// </summary>
    /// <remarks>
    /// An opener with no closer is left alone rather than reported. It is indistinguishable from a value
    /// that happens to contain two braces, and there is no name to tell the administrator about.
    /// </remarks>
    private static IEnumerable<string> Tokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var position = 0;

        while (position < text.Length)
        {
            var open = text.IndexOf(Open, position, StringComparison.Ordinal);
            if (open < 0)
                yield break;

            if (IsEscaped(text, open))
            {
                position = open + EscapedOpen.Length;
                continue;
            }

            var close = text.IndexOf(Close, open + Open.Length, StringComparison.Ordinal);
            if (close < 0)
                yield break;

            yield return text[(open + Open.Length)..close].Trim();

            position = close + Close.Length;
        }
    }

    private static bool IsEscaped(string text, int open) =>
        text.AsSpan(open).StartsWith(EscapedOpen, StringComparison.Ordinal);
}
