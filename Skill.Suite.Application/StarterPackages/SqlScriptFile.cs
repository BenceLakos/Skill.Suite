namespace Skill.Suite.Application.StarterPackages;

/// <summary>
/// The one kind of file a session's database seed script may be.
/// </summary>
/// <remarks>
/// One constant rather than the literal at each site, because the same extension decides three separate
/// things: which files the picker offers, which paths the session validators accept, and nothing else on the
/// volume being read as a script. Matched case-insensitively — an author's export is as likely to be
/// <c>INIT.SQL</c> as <c>init.sql</c>, and the filesystem under the volume may or may not care.
/// </remarks>
public static class SqlScriptFile
{
    public const string Extension = ".sql";

    public static bool Matches(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
}
