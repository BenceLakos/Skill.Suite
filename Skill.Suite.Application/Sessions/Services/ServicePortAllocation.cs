namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Gives each competitor's copy of a per-competitor service its own host ports.
/// </summary>
/// <remarks>
/// <see cref="PortMapping.HostPort"/> is a fixed port on the competition machine, so N copies of one service
/// all publishing it would leave the first competitor's container running and every other one failing to
/// start. The configured port is therefore read as a BASE and the competitor's ordinal is added to it:
/// competitor 0 gets the port the administrator typed, competitor 1 the next one, and so on.
/// <para>
/// Arithmetic on a stored ordinal rather than a port the daemon hands out, because the allocation has to
/// survive two things a counter cannot. Restarting the session must give a competitor the port they have
/// already written down, and a competitor enrolled or removed a day later must not renumber anybody else —
/// which is exactly what <see cref="SessionCompetitor.Ordinal"/> guarantees by being assigned once, at
/// enrolment, and never reused.
/// </para>
/// </remarks>
internal static class ServicePortAllocation
{
    /// <summary>The highest port TCP and UDP have, and the ceiling the base plus the ordinal must stay under.</summary>
    internal const int MaxPort = 65535;

    /// <summary>
    /// <paramref name="configured"/> shifted up by <paramref name="ordinal"/>, or a failure naming the
    /// mapping that would leave the port range.
    /// </summary>
    /// <remarks>
    /// Reported as that competitor's failure rather than refusing the whole start: a base of 65530 with
    /// twenty competitors is a mistake that costs the last few of them their service, and denying the other
    /// fifteen theirs would not help anybody.
    /// </remarks>
    public static Result<IReadOnlyList<PortMapping>> For(IReadOnlyList<PortMapping> configured, int ordinal)
    {
        var allocated = new List<PortMapping>(configured.Count);

        foreach (var mapping in configured)
        {
            var hostPort = (long)mapping.HostPort + ordinal;

            if (hostPort > MaxPort)
                return Result.Failure<IReadOnlyList<PortMapping>>(
                    SessionErrors.ServicePortOutOfRange(mapping.HostPort, ordinal));

            allocated.Add(mapping with { HostPort = (int)hostPort });
        }

        return Result.Success<IReadOnlyList<PortMapping>>(allocated);
    }
}
