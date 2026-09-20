namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// Everything the signed-in competitor is told about their own participation.
/// </summary>
/// <remarks>
/// The credentials are outside <see cref="Session"/> deliberately: they are the competitor's whether or not a
/// session is running, and a page that showed nothing between sessions would be the page they are told to
/// check when they cannot sign in somewhere else.
/// </remarks>
/// <param name="Session">
/// The active session, or null both when none is running and when this competitor is not taking part in the
/// one that is.
/// </param>
/// <param name="NotEnrolled">
/// Tells those two apart for the reader. Nothing else changes: a competitor who is not enrolled is shown the
/// same nothing as one with no session at all, because a session they are not in has no credentials, database
/// or services of theirs to show.
/// </param>
public sealed record MySessionOverviewDto(
    MySessionCredentialsDto Credentials,
    MySessionDetailsDto? Session,
    bool NotEnrolled);
