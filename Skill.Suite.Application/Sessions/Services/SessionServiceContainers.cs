namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// Every container name a session's images resolve to, without resolving anything they contain.
/// </summary>
/// <remarks>
/// What stopping a session needs and planning would be the wrong tool for: stopping acts on names, and the
/// values behind the placeholders — a competitor's password, their connection string — have no business
/// being unprotected to stop a container.
/// <para>
/// Deliberately names a container for EVERY enrolled competitor of every image, including competitors
/// starting skipped for having no SQL login. Stopping a container that was never created is a no-op the
/// daemon reports as absent, whereas leaving one running is a port still held and a service a competitor can
/// still reach after the session was stopped.
/// </para>
/// </remarks>
internal static class SessionServiceContainers
{
    public static IReadOnlyList<SessionServiceContainer> For(
        Session session, IReadOnlyList<string> usernames, SessionRunMode mode)
    {
        var containers = new List<SessionServiceContainer>();

        for (var index = 0; index < session.DockerImages.Count; index++)
        {
            var image = session.DockerImages[index];

            containers.AddRange(usernames.Select(username => new SessionServiceContainer(
                SessionServiceNaming.ContainerName(session.Slug, index, username, mode),
                image.Image,
                username)));
        }

        return containers;
    }
}
