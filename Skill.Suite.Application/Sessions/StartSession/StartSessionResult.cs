namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// What starting the session managed to do, per stage.
/// </summary>
/// <remarks>
/// Reports partial success rather than a bare pass/fail: with twenty competitors, three services and a shared
/// database, "starting failed" is useless to whoever has to fix it before the competition begins.
/// <see cref="Failed"/> carries the external system's own message per item, tagged with the stage it came
/// from.
/// <para>
/// The two skipped lists are separate from <see cref="Failed"/> and deliberately outside
/// <see cref="FullySucceeded"/>. Those competitors have no account on the system in question, so nothing was
/// attempted for them and nothing went wrong — but they are the difference between "everyone is ready" and
/// "everyone who could be is", which is why they are reported rather than passed over in silence.
/// </para>
/// </remarks>
public sealed record StartSessionResult(
    string Organization,
    int Provisioned,
    int DatabaseAccessGranted,
    int ServicesRunning,
    IReadOnlyList<SessionProvisioningFailure> Failed,
    IReadOnlyList<string> SkippedNoGitAccess,
    IReadOnlyList<string> SkippedNoDatabaseLogin)
{
    public bool FullySucceeded => Failed.Count == 0;
}
