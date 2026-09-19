namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// One thing starting the session could not do, and why.
/// </summary>
/// <remarks>
/// Carries the stage because the subject alone is ambiguous once more than one stage can fail: "c07" reads as
/// a repository or as a database grant depending on which stage produced it, and the two need different
/// repairs. <see cref="Subject"/> is whatever the stage was working on — a competitor username, an image
/// reference, or the session database.
/// </remarks>
public sealed record StartSessionFailure(StartSessionStage Stage, string Subject, string Error);
