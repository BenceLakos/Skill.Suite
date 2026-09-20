namespace Skill.Suite.Application.Sessions.StartSession;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Competitors;

/// <summary>
/// Everything the database stage needs, worked out before the session is flipped to Active.
/// </summary>
/// <remarks>
/// Built up front so the two things that can make the whole stage impossible — no SQL Server credential, and
/// a server whose login list cannot be read — refuse the Start while nothing has been created yet, instead of
/// surfacing as N identical per-competitor failures after the repositories exist.
/// </remarks>
/// <param name="BaseName">
/// The session's database name, which names no database of its own: each competitor's is
/// <c>SessionDatabaseNaming.For(BaseName, username)</c>, and is theirs alone.
/// </param>
/// <param name="Seed">
/// The script to run against each competitor's database when this run is the one that creates it, or null
/// when the session has none. Read here rather than at the point of use, so an unreadable script refuses the
/// Start rather than failing once per competitor — and read ONCE for all of them, since the same text is
/// what every one of those databases is seeded with.
/// </param>
/// <param name="WithLogin">Competitors who hold a SQL login, so a database and a grant are worth creating.</param>
/// <param name="SkippedNoDatabaseLogin">
/// Competitors who get a repository but no database, because the SQL Server has no login for them.
/// </param>
internal sealed record DatabaseAccessPlan(
    string BaseName,
    bool Read,
    bool Write,
    BasicCredential Admin,
    SeedScript? Seed,
    IReadOnlyList<Competitor> WithLogin,
    IReadOnlyList<string> SkippedNoDatabaseLogin);
