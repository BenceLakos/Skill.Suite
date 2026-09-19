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
/// <param name="WithLogin">Competitors who hold a SQL login, so a grant against it can succeed.</param>
/// <param name="SkippedNoDatabaseLogin">
/// Competitors who get a repository but no grant, because the SQL Server has no login for them.
/// </param>
internal sealed record DatabaseAccessPlan(
    string Database,
    bool Read,
    bool Write,
    BasicCredential Admin,
    IReadOnlyList<Competitor> WithLogin,
    IReadOnlyList<string> SkippedNoDatabaseLogin);
