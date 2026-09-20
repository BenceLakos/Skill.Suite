namespace Skill.Suite.Application.Sessions;

using System.Globalization;
using Skill.Suite.Application.Sessions.Services;

/// <summary>
/// Names a session's service containers, from the session slug and the image's position in its list.
/// </summary>
/// <remarks>
/// Derived rather than generated, and derived from nothing that changes between runs. The container name is
/// the only thing <c>EnsureRunningAsync</c> has for deciding whether a service is already up, so a name that
/// varied per call would start a second copy of every service on each Start — and the second copy would fail
/// on the host ports the first one already publishes.
/// <para>
/// The position is used rather than the image reference because an image reference contains slashes, colons
/// and digests, none of which a container name may hold, and two services of one session may legitimately run
/// the same image with different ports.
/// </para>
/// <para>
/// Every container belongs to exactly one competitor, so every name carries their username; marking adds a
/// fixed suffix on top. Both container sets one session can produce are therefore nameable, distinct, and
/// reconstructible from values that cannot drift.
/// </para>
/// </remarks>
internal static class SessionServiceNaming
{
    /// <summary>Marks the container as this application's, so it is recognisable in <c>docker ps</c>.</summary>
    private const string ContainerNamePrefix = "skill-suite";

    /// <summary>The one character docker allows here that reads as a separator.</summary>
    private const string Separator = "-";

    /// <summary>Services are numbered the way the admin sees them listed, from one.</summary>
    private const int FirstServiceNumber = 1;

    /// <summary>
    /// Distinguishes the containers marking runs from the ones the competition ran.
    /// </summary>
    /// <remarks>
    /// A suffix rather than a separate prefix so the session slug still leads the name and
    /// <c>docker ps</c> keeps a session's containers together, whichever set they belong to.
    /// </remarks>
    private const string MarkingSuffix = "marking";

    public static string ContainerName(
        string slug, int index, string competitorUsername, SessionRunMode mode)
    {
        var name =
            $"{ContainerNamePrefix}{Separator}{slug}{Separator}{ServiceNumber(index)}"
            + Separator + competitorUsername;

        return mode == SessionRunMode.Marking ? name + Separator + MarkingSuffix : name;
    }

    /// <summary>The service's 1-based position, as the service label carries it.</summary>
    public static string ServiceNumber(int index) =>
        (index + FirstServiceNumber).ToString(CultureInfo.InvariantCulture);
}
