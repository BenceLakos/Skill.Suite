namespace Skill.Suite.Application.Sessions;

/// <summary>
/// The docker labels every service container of a session is stamped with.
/// </summary>
/// <remarks>
/// <see cref="SessionKey"/> is the only handle closing a session has on the containers starting it created:
/// container names can be reconstructed, but only for the images the session still carries, so an image the
/// admin removed from the session before closing it would leave its container running forever. Finding them
/// by label finds those too, including the ones a previous run of this application started.
/// </remarks>
internal static class SessionServiceLabels
{
    /// <summary>Carries the session slug. Removal at close filters on this.</summary>
    public const string SessionKey = "skill-suite.session";

    /// <summary>Carries the service's 1-based position in the session's image list.</summary>
    public const string ServiceKey = "skill-suite.service";
}
