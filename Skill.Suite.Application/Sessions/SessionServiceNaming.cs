namespace Skill.Suite.Application.Sessions;

using System.Globalization;

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
/// </remarks>
internal static class SessionServiceNaming
{
    /// <summary>Marks the container as this application's, so it is recognisable in <c>docker ps</c>.</summary>
    private const string ContainerNamePrefix = "skill-suite";

    /// <summary>The one character docker allows here that reads as a separator.</summary>
    private const string Separator = "-";

    /// <summary>Services are numbered the way the admin sees them listed, from one.</summary>
    private const int FirstServiceNumber = 1;

    public static string ContainerName(string slug, int index) =>
        $"{ContainerNamePrefix}{Separator}{slug}{Separator}{ServiceNumber(index)}";

    /// <summary>The service's 1-based position, as the service label carries it.</summary>
    public static string ServiceNumber(int index) =>
        (index + FirstServiceNumber).ToString(CultureInfo.InvariantCulture);
}
