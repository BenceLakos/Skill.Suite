namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// What provisioning managed to do, per competitor.
/// </summary>
/// <remarks>
/// Reports partial success rather than a bare pass/fail: with twenty competitors, "provisioning failed" is
/// useless to whoever has to fix it before the competition starts. <see cref="Failed"/> carries the
/// git host's own message per competitor.
/// </remarks>
public sealed record StartSessionResult(
    string Organization,
    int Provisioned,
    IReadOnlyList<StartSessionFailure> Failed)
{
    public bool FullySucceeded => Failed.Count == 0;
}
