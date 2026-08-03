namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Expands the configured <see cref="WebhookOptions.FolderTemplate"/> into a concrete
/// per-submission folder name. Substitution is intentionally tiny — no regex engines,
/// no expression language; the template is operator-supplied config.
/// </summary>
public static class SubmissionFolderNameGenerator
{
    public static string Generate(string template, string? competitor, string? commitSha)
    {
        var random = Guid.NewGuid().ToString("N")[..12];
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var result = template
            .Replace("{random}", random)
            .Replace("{competitor}", Sanitize(competitor) ?? "anon")
            .Replace("{commit}", string.IsNullOrWhiteSpace(commitSha) ? "" : commitSha[..Math.Min(8, commitSha.Length)])
            .Replace("{timestamp}", timestamp);

        return Sanitize(result) ?? random;
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                buffer[i++] = c;
        }

        return i == 0 ? null : new string(buffer[..i]);
    }
}
