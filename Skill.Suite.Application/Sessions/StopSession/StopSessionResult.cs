namespace Skill.Suite.Application.Sessions.StopSession;

/// <summary>
/// The session is stopped; this is what was withdrawn on the way.
/// </summary>
/// <remarks>
/// Reports partial success rather than a bare pass/fail, for the same reason starting does: with twenty
/// competitors and three services, "stopping failed" tells whoever has to finish the job by hand nothing.
/// <see cref="Failed"/> carries the external system's own message per item, tagged with the stage it came
/// from.
/// </remarks>
/// <param name="AccessRevoked">
/// Competitor repositories the git host confirmed access on, including the ones that turned out to have no
/// grant left to remove — the end state is what matters, not whether this call is the one that produced it.
/// </param>
/// <param name="ServicesStopped">Service containers the docker daemon confirmed are not running.</param>
public sealed record StopSessionResult(
    int AccessRevoked,
    int ServicesStopped,
    IReadOnlyList<SessionProvisioningFailure> Failed)
{
    public bool FullySucceeded => Failed.Count == 0;
}
