namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// What a service's configuration asks the platform for, read without resolving any of it.
/// </summary>
/// <remarks>
/// Two questions come out of one pass. <see cref="Unknown"/> is what the validators refuse a save over, and
/// <see cref="NeedsDatabase"/> is whether the service can be started for a competitor who has no session
/// database — scanned separately they could disagree about the same text.
/// <para>
/// Nothing here decides HOW MANY containers a service becomes. Every service is one container per
/// competitor; the placeholders only decide what differs between them.
/// </para>
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
    /// <summary>
    /// Whether this service needs a competitor's session database, which not every competitor has.
    /// </summary>
    public bool NeedsDatabase =>
        Used.Any(placeholder => ServicePlaceholders.ScopeOf(placeholder) == ServicePlaceholderScope.Database);
}
