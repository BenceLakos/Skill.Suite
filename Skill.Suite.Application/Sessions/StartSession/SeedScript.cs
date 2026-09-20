namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// The session's database seed script, already read off the starter packages volume.
/// </summary>
/// <remarks>
/// The path travels with the text because the two are used for different things and neither can stand in for
/// the other: the text is what runs, while the path is what a log line and a failure row have to name — an
/// administrator repairs the script on the volume, not the copy in memory.
/// </remarks>
internal sealed record SeedScript(string Path, string Sql);
