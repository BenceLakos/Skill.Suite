namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// What a <see cref="ServicePlaceholder"/> depends on, which is what decides how many containers a service
/// configured with it is started as.
/// </summary>
/// <remarks>
/// The scope is the whole rule. A service whose configuration mentions nothing beyond
/// <see cref="Session"/> has one answer for everybody, so it stays the single shared container it has always
/// been; anything <see cref="Competitor"/>- or <see cref="Database"/>-scoped has a different answer per
/// competitor, and one shared container could only ever carry one of them.
/// </remarks>
public enum ServicePlaceholderScope
{
    /// <summary>The same value for every competitor in the session.</summary>
    Session,

    /// <summary>A value belonging to one competitor.</summary>
    Competitor,

    /// <summary>
    /// A value belonging to one competitor's session database, which only exists for a competitor who holds
    /// a SQL login.
    /// </summary>
    Database,
}
