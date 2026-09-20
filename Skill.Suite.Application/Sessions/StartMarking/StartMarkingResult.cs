namespace Skill.Suite.Application.Sessions.StartMarking;

/// <summary>
/// What starting one competitor's marking managed to bring up, and where each of it is.
/// </summary>
/// <remarks>
/// Reports partial success for the reason starting a session does: with several services, "marking failed"
/// tells the expert who has to mark them nothing. <see cref="Failed"/> carries the daemon's own message per
/// container.
/// </remarks>
/// <param name="Endpoints">
/// Only the containers that are actually running. An address for a container that failed to start would be
/// worse than none at all — it reads as a marking setup that is ready.
/// </param>
/// <param name="DatabaseServicesSkipped">
/// True when this competitor's database-backed services were not started, because the SQL Server holds no
/// login for them and so no session database of theirs was ever created. Nothing was attempted, so it is
/// separate from <see cref="Failed"/> — but it is the difference between "this competitor can be marked" and
/// "the part of them that does not need a database can".
/// </param>
public sealed record StartMarkingResult(
    string CompetitorUsername,
    string MarkingIpAddress,
    int ServicesRunning,
    IReadOnlyList<MarkingEndpoint> Endpoints,
    IReadOnlyList<SessionProvisioningFailure> Failed,
    bool DatabaseServicesSkipped)
{
    public bool FullySucceeded => Failed.Count == 0;
}
