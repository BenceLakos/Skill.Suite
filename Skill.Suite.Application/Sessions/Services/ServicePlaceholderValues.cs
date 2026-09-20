namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions.MySession;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Resolves the catalogue against one session, one competitor and one run mode.
/// </summary>
/// <remarks>
/// The one place the competition and marking differ in substance. Competitor-scoped values are the
/// competitor's in both modes — a marking container still has to know whose work it is showing — while every
/// database-scoped value swaps the login: competitors connect as themselves during the competition, and an
/// expert marking afterwards connects as the SQL Server administrator, against that same competitor's
/// database. Marking with the competitor's own login would show the marker exactly as much as the
/// competitor's grants allow, which on a read-only session is nothing worth marking.
/// </remarks>
internal static class ServicePlaceholderValues
{
    /// <summary>What a shared service can ask for: the values that are the same for everyone.</summary>
    public static IReadOnlyDictionary<ServicePlaceholder, string> ForSession(Session session) =>
        new Dictionary<ServicePlaceholder, string>
        {
            [ServicePlaceholder.SessionName] = session.Name,
            [ServicePlaceholder.SessionSlug] = session.Slug,
        };

    /// <summary>
    /// The full catalogue for one competitor, including their database as this mode addresses it.
    /// </summary>
    /// <remarks>
    /// A session with no database name resolves the database values to empty strings rather than refusing.
    /// The validators stop a database-scoped service being saved without one, and the planner starts such a
    /// service for nobody, so this is only reachable for a session configured before either existed — where
    /// an empty value is still better than a literal <c>{{database.name}}</c> reaching a connection string.
    /// </remarks>
    public static IReadOnlyDictionary<ServicePlaceholder, string> For(
        Session session,
        SessionServicePlanCompetitor competitor,
        SessionRunMode mode,
        string? databaseBaseName,
        string? databaseServer,
        BasicCredential? databaseAdmin)
    {
        var login = mode == SessionRunMode.Marking && databaseAdmin is not null
            ? databaseAdmin.Username
            : competitor.Username;

        var password = mode == SessionRunMode.Marking && databaseAdmin is not null
            ? databaseAdmin.Secret
            : competitor.Password;

        var database = string.IsNullOrWhiteSpace(databaseBaseName)
            ? string.Empty
            : SessionDatabaseNaming.For(databaseBaseName, competitor.Username);

        var server = databaseServer ?? string.Empty;

        return new Dictionary<ServicePlaceholder, string>
        {
            [ServicePlaceholder.SessionName] = session.Name,
            [ServicePlaceholder.SessionSlug] = session.Slug,
            [ServicePlaceholder.CompetitorUsername] = competitor.Username,
            [ServicePlaceholder.CompetitorFullName] = competitor.FullName,
            [ServicePlaceholder.CompetitorIpAddress] = competitor.IpAddress,
            [ServicePlaceholder.CompetitorCountryCode] = competitor.CountryCode,
            [ServicePlaceholder.CompetitorPassword] = competitor.Password,
            [ServicePlaceholder.DatabaseName] = database,
            [ServicePlaceholder.DatabaseServer] = server,
            [ServicePlaceholder.DatabaseLogin] = login,
            [ServicePlaceholder.DatabasePassword] = password,
            [ServicePlaceholder.DatabaseConnectionString] =
                MsSqlConnectionString.For(server, database, login, password) ?? string.Empty,
        };
    }
}
