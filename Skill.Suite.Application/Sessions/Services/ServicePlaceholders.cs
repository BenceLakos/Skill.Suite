namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// The catalogue: every <see cref="ServicePlaceholder"/>, the one name it is written as, and its scope.
/// </summary>
/// <remarks>
/// One table rather than a name on each side of the feature. The validators reject a name that is not in
/// here, the planner resolves the names that are, and the session form lists them for the administrator —
/// all from this, so a placeholder cannot be offered on the form and then arrive at a container unresolved.
/// <para>
/// Names are matched case-insensitively. A session author typing <c>{{Competitor.Username}}</c> has made no
/// mistake worth refusing a save over, and refusing it would only teach them that the catalogue is
/// capricious.
/// </para>
/// </remarks>
public static class ServicePlaceholders
{
    private static readonly Dictionary<ServicePlaceholder, string> NamesByPlaceholder = new()
    {
        [ServicePlaceholder.SessionName] = "session.name",
        [ServicePlaceholder.SessionSlug] = "session.slug",
        [ServicePlaceholder.CompetitorUsername] = "competitor.username",
        [ServicePlaceholder.CompetitorFullName] = "competitor.fullName",
        [ServicePlaceholder.CompetitorIpAddress] = "competitor.ipAddress",
        [ServicePlaceholder.CompetitorMobileIpAddress] = "competitor.mobileIpAddress",
        [ServicePlaceholder.CompetitorCountryCode] = "competitor.countryCode",
        [ServicePlaceholder.CompetitorPassword] = "competitor.password",
        [ServicePlaceholder.DatabaseName] = "database.name",
        [ServicePlaceholder.DatabaseServer] = "database.server",
        [ServicePlaceholder.DatabaseLogin] = "database.login",
        [ServicePlaceholder.DatabasePassword] = "database.password",
        [ServicePlaceholder.DatabaseConnectionString] = "database.connectionString",
    };

    private static readonly Dictionary<ServicePlaceholder, ServicePlaceholderScope> ScopesByPlaceholder = new()
    {
        [ServicePlaceholder.SessionName] = ServicePlaceholderScope.Session,
        [ServicePlaceholder.SessionSlug] = ServicePlaceholderScope.Session,
        [ServicePlaceholder.CompetitorUsername] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.CompetitorFullName] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.CompetitorIpAddress] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.CompetitorMobileIpAddress] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.CompetitorCountryCode] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.CompetitorPassword] = ServicePlaceholderScope.Competitor,
        [ServicePlaceholder.DatabaseName] = ServicePlaceholderScope.Database,
        [ServicePlaceholder.DatabaseServer] = ServicePlaceholderScope.Database,
        [ServicePlaceholder.DatabaseLogin] = ServicePlaceholderScope.Database,
        [ServicePlaceholder.DatabasePassword] = ServicePlaceholderScope.Database,
        [ServicePlaceholder.DatabaseConnectionString] = ServicePlaceholderScope.Database,
    };

    private static readonly Dictionary<string, ServicePlaceholder> PlaceholdersByName =
        NamesByPlaceholder.ToDictionary(entry => entry.Value, entry => entry.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every placeholder in catalogue order, for the form to list.</summary>
    public static IReadOnlyList<ServicePlaceholder> All { get; } = [.. NamesByPlaceholder.Keys];

    /// <summary>The name this placeholder is written as, without the braces.</summary>
    public static string NameOf(ServicePlaceholder placeholder) => NamesByPlaceholder[placeholder];

    /// <summary>The name this placeholder is written as, braces and all, as the form shows it.</summary>
    public static string TokenOf(ServicePlaceholder placeholder) =>
        ServiceTemplate.Open + NamesByPlaceholder[placeholder] + ServiceTemplate.Close;

    public static ServicePlaceholderScope ScopeOf(ServicePlaceholder placeholder) =>
        ScopesByPlaceholder[placeholder];

    public static bool TryParse(string name, out ServicePlaceholder placeholder) =>
        PlaceholdersByName.TryGetValue(name, out placeholder);
}
