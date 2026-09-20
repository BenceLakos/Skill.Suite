namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// What a service's configuration asks the platform for, read without resolving any of it.
/// </summary>
/// <remarks>
/// Two questions come out of one pass, and both have to agree. <see cref="Unknown"/> is what the validators
/// refuse a save over, and <see cref="IsPerCompetitor"/> is how many containers the service is started as —
/// scanned separately they could disagree about the same text, which is a service the form accepts and the
/// start handler cannot decide the shape of.
/// </remarks>
/// <param name="Used">Every catalogued placeholder the service mentions, each once, in order of appearance.</param>
/// <param name="Unknown">
/// Names written as placeholders that the catalogue does not hold. Reported rather than ignored: an unknown
/// name reaches the container verbatim, and a connection string with <c>{{database.sever}}</c> in it fails
/// as a hostname during the competition rather than as a typo on the form.
/// </param>
internal sealed record ServiceTemplateScan(
    IReadOnlyList<ServicePlaceholder> Used,
    IReadOnlyList<string> Unknown)
{
    /// <summary>A service that mentions nothing at all — the shared container this feature started from.</summary>
    public static readonly ServiceTemplateScan Empty = new([], []);

    /// <summary>
    /// Whether this service is started once per competitor rather than once for the session.
    /// </summary>
    /// <remarks>
    /// True as soon as anything competitor- or database-scoped is mentioned, because one shared container
    /// could only carry one competitor's answer. A service that names only session-scoped values has one
    /// answer for everybody and stays exactly the single container it was before placeholders existed, which
    /// is what keeps every session configured so far working unchanged.
    /// </remarks>
    public bool IsPerCompetitor =>
        Used.Any(placeholder => ServicePlaceholders.ScopeOf(placeholder) != ServicePlaceholderScope.Session);

    /// <summary>
    /// Whether this service needs a competitor's session database, which not every competitor has.
    /// </summary>
    public bool NeedsDatabase =>
        Used.Any(placeholder => ServicePlaceholders.ScopeOf(placeholder) == ServicePlaceholderScope.Database);
}
