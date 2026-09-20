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

    /// <summary>
    /// Carries the username of the competitor a per-competitor container belongs to. Absent on shared ones.
    /// </summary>
    /// <remarks>
    /// Not needed to find the container — the name already carries the username — but it is what makes
    /// <c>docker ps --filter</c> able to answer "whose container is this?" without parsing names, which is
    /// the question an expert asks at the machine while the competition is running.
    /// </remarks>
    public const string CompetitorKey = "skill-suite.competitor";

    /// <summary>
    /// Present only on marking containers, and carrying the session slug rather than a bare flag.
    /// </summary>
    /// <remarks>
    /// The slug is the value, not the key, so stopping marking is one label filter: the daemon is asked for
    /// containers marked as marking FOR THIS SESSION, which cannot catch another session's marking run and
    /// cannot catch this session's competition containers. A bare <c>true</c> would have needed two filters
    /// and a wider interface to pass them through.
    /// </remarks>
    public const string MarkingKey = "skill-suite.marking";
}
