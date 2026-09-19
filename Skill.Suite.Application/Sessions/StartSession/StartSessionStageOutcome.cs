namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// How much of one stage's work got through, and what did not.
/// </summary>
/// <remarks>
/// <see cref="Succeeded"/> is not the item count minus the failure count. A stage can fail once in a way that
/// takes the rest of its items with it — a session database that could not be created leaves no grant to
/// attempt — so the two numbers are reported independently rather than derived from one another.
/// </remarks>
internal sealed record StartSessionStageOutcome(int Succeeded, IReadOnlyList<StartSessionFailure> Failures);
