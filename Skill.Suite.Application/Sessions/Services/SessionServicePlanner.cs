namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Application.DockerImages;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Turns a session's configured images into the exact list of containers to run.
/// </summary>
/// <remarks>
/// Pure, and deliberately the only thing that answers "which containers". Start Session, Start Marking and —
/// through <see cref="SessionServiceContainers"/> — Stop Session all derive their container names the same
/// way, so a container cannot be started under one name and looked for under another.
/// <para>
/// THE RULE, in one sentence and repeated on the session form: every service of a session is started once
/// per competitor, exactly as their session database is. Nothing is shared — not a service with no
/// placeholders, not one with no domain — because a competition is N independent workspaces, and one
/// container between twenty people is one competitor's restart costing the other nineteen their work. The
/// placeholders exist to let the settings DIFFER per competitor, not to decide how many containers there
/// are.
/// </para>
/// <para>
/// The one exception is a subtraction, not a sharing: a service whose settings mention a database-scoped
/// placeholder is skipped for a competitor the SQL Server holds no login for, because no session database of
/// theirs was ever created for it to be pointed at. That competitor is reported, not failed.
/// </para>
/// <para>
/// A service with a domain also gets the reverse-proxy labels and joins the proxy's network. Labels the
/// administrator wrote themselves win over the generated ones key for key — an author who has typed
/// <c>traefik.http.routers.x.rule</c> means it — while the platform's own session, service, competitor and
/// marking labels win over both, because they are what tearing the session down finds the containers by.
/// </para>
/// </remarks>
internal static class SessionServicePlanner
{
    public static SessionServicePlan Plan(SessionServicePlanRequest request)
    {
        var session = request.Session;
        var services = new List<PlannedSessionService>();
        var failures = new List<SessionProvisioningFailure>();
        var skipped = new List<string>();

        for (var index = 0; index < session.DockerImages.Count; index++)
        {
            var image = session.DockerImages[index];
            var needsDatabase = ServiceTemplate.Scan(image).NeedsDatabase;
            var daemonImage = DaemonImageReference.ForDaemon(image.Image, request.GitInternalBaseUrl);

            foreach (var competitor in request.Competitors)
            {
                if (needsDatabase && !competitor.HasDatabaseLogin)
                {
                    if (!skipped.Contains(competitor.Username, StringComparer.OrdinalIgnoreCase))
                        skipped.Add(competitor.Username);

                    continue;
                }

                var ports = ServicePortAllocation.For(image.PortMappings, competitor.Ordinal);

                if (ports.IsFailure)
                {
                    failures.Add(new SessionProvisioningFailure(
                        request.Stage, competitor.Username, ports.Error.Message));

                    continue;
                }

                services.Add(ForCompetitor(request, image, daemonImage, index, competitor, ports.Value));
            }
        }

        return new SessionServicePlan(services, failures, skipped);
    }

    private static PlannedSessionService ForCompetitor(
        SessionServicePlanRequest request,
        SessionDockerImage image,
        string daemonImage,
        int index,
        SessionServicePlanCompetitor competitor,
        IReadOnlyList<PortMapping> ports)
    {
        var values = ServicePlaceholderValues.For(
            request.Session,
            competitor,
            request.Mode,
            request.DatabaseBaseName,
            request.DatabaseServer,
            request.DatabaseAdmin);

        var containerName = SessionServiceNaming.ContainerName(
            request.Session.Slug, index, competitor.Username, request.Mode);

        var route = RouteFor(request, image, containerName, ClientIpsFor(request, competitor));

        return new PlannedSessionService(
            SessionServiceNaming.ServiceNumber(index),
            daemonImage,
            image.Image,
            containerName,
            competitor.Username,
            Render(image.Env, values),
            Labels(request, image, index, values, competitor.Username, route),
            Volumes(image.Volumes, values),
            ports,
            route is null ? null : request.ServiceNetwork,
            route?.Host);
    }

    /// <summary>
    /// Whose machines may reach this container.
    /// </summary>
    /// <remarks>
    /// While the competition runs, the competitor's own: their workstation and, if they were given one,
    /// their mobile device. While marking, the machine the expert is marking from — plus that competitor's
    /// mobile device again, so a phone the task is meant to be demonstrated on can still reach the container
    /// being marked. The competitor's WORKSTATION is deliberately not on the marking list: the competition
    /// is over, and leaving it there would let them keep using the instance an expert is judging.
    /// </remarks>
    private static List<string> ClientIpsFor(
        SessionServicePlanRequest request, SessionServicePlanCompetitor competitor)
    {
        var addresses = new List<string>();

        if (request.Mode == SessionRunMode.Marking)
        {
            if (!string.IsNullOrWhiteSpace(request.MarkingIpAddress))
                addresses.Add(request.MarkingIpAddress);
        }
        else
        {
            addresses.Add(competitor.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(competitor.MobileIpAddress))
            addresses.Add(competitor.MobileIpAddress);

        return addresses;
    }

    /// <summary>
    /// The container's route through the proxy, or null when it is not routed.
    /// </summary>
    /// <remarks>
    /// The service's own routed port is what the proxy forwards to, and the session validators make sure a
    /// domain cannot be saved without one. Nothing here reads the port MAPPINGS: the proxy reaches the
    /// container over the shared docker network and talks to the port inside it, so a routed service needs
    /// nothing published on the host at all.
    /// <para>
    /// Every route carries at least one source address, because every container belongs to exactly one
    /// competitor. A route without one would be one competitor's container answering for the whole domain,
    /// which is how twenty people end up looking at the first competitor's work.
    /// </para>
    /// </remarks>
    private static TraefikRoute? RouteFor(
        SessionServicePlanRequest request,
        SessionDockerImage image,
        string containerName,
        IReadOnlyList<string> clientIps) =>
        string.IsNullOrWhiteSpace(image.Domain) || image.RoutedPort is not { } routedPort
            ? null
            : new TraefikRoute(
                containerName, image.Domain, clientIps, routedPort, request.ServiceNetwork);

    /// <summary>
    /// The image's own labels, rendered, then the proxy's, then the platform's.
    /// </summary>
    /// <remarks>
    /// In that order, and the order is the precedence. An administrator who typed a Traefik label themselves
    /// has overridden the generated one deliberately; the platform's own win over everybody, because they
    /// are what Close and Stop Marking find the containers by and a session label an image happened to carry
    /// its own value for would strand its container.
    /// </remarks>
    private static Dictionary<string, string> Labels(
        SessionServicePlanRequest request,
        SessionDockerImage image,
        int index,
        IReadOnlyDictionary<ServicePlaceholder, string> values,
        string competitorUsername,
        TraefikRoute? route)
    {
        var labels = Render(image.Labels, values);

        if (route is not null)
        {
            foreach (var label in TraefikLabels.For(route))
                labels.TryAdd(label.Key, label.Value);
        }

        labels[SessionServiceLabels.SessionKey] = request.Session.Slug;
        labels[SessionServiceLabels.ServiceKey] = SessionServiceNaming.ServiceNumber(index);
        labels[SessionServiceLabels.CompetitorKey] = competitorUsername;

        // Carries the slug rather than a bare "true" so Stop Marking can remove one session's marking
        // containers with a label filter, and leave another session's alone.
        if (request.Mode == SessionRunMode.Marking)
            labels[SessionServiceLabels.MarkingKey] = request.Session.Slug;

        return labels;
    }

    private static Dictionary<string, string> Render(
        IReadOnlyDictionary<string, string> source, IReadOnlyDictionary<ServicePlaceholder, string> values) =>
        source.ToDictionary(entry => entry.Key, entry => ServiceTemplate.Render(entry.Value, values));

    private static List<VolumeMount> Volumes(
        IReadOnlyList<VolumeMount> source, IReadOnlyDictionary<ServicePlaceholder, string> values) =>
        [.. source.Select(volume => volume with { HostPath = ServiceTemplate.Render(volume.HostPath, values) })];
}
