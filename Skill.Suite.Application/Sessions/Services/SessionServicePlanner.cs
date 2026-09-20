namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Application.DockerImages;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Turns a session's configured images into the exact list of containers to run.
/// </summary>
/// <remarks>
/// Pure, and deliberately the only thing that answers "how many containers". Start Session, Start Marking
/// and — through <see cref="SessionServiceContainers"/> — Stop Session all read the shape of a service from
/// the same scan, so a service cannot be started per competitor and stopped as though it were shared.
/// <para>
/// THE RULE, in one sentence and repeated on the session form: a service whose environment values, label
/// values or volume host paths mention any competitor- or database-scoped placeholder is started once per
/// competitor enrolled in the session, except that a service mentioning a database-scoped placeholder is
/// started only for those of them the SQL Server holds a login for — the rest have no session database for
/// it to be pointed at — and a service mentioning neither stays the single shared container it has always
/// been.
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
            var scan = ServiceTemplate.Scan(image);
            var daemonImage = DaemonImageReference.ForDaemon(image.Image, request.GitInternalBaseUrl);

            if (!scan.IsPerCompetitor)
            {
                services.Add(Shared(request, image, daemonImage, index));
                continue;
            }

            foreach (var competitor in request.Competitors)
            {
                if (scan.NeedsDatabase && !competitor.HasDatabaseLogin)
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

    /// <summary>
    /// The one container a service with no competitor-scoped placeholder runs as.
    /// </summary>
    /// <remarks>
    /// Still rendered, because <c>{{session.slug}}</c> and <c>{{session.name}}</c> are legitimate here and
    /// have one answer. The name and labels are exactly what sessions produced before this feature, so a
    /// session already running keeps its containers across an upgrade instead of gaining a second copy.
    /// </remarks>
    private static PlannedSessionService Shared(
        SessionServicePlanRequest request, SessionDockerImage image, string daemonImage, int index)
    {
        var values = ServicePlaceholderValues.ForSession(request.Session);

        return new PlannedSessionService(
            SessionServiceNaming.ServiceNumber(index),
            daemonImage,
            image.Image,
            SessionServiceNaming.ContainerName(request.Session.Slug, index, competitorUsername: null, request.Mode),
            CompetitorUsername: null,
            Render(image.Env, values),
            Labels(request, index, values, competitorUsername: null),
            Volumes(image.Volumes, values),
            image.PortMappings);
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

        return new PlannedSessionService(
            SessionServiceNaming.ServiceNumber(index),
            daemonImage,
            image.Image,
            SessionServiceNaming.ContainerName(request.Session.Slug, index, competitor.Username, request.Mode),
            competitor.Username,
            Render(image.Env, values),
            Labels(request, index, values, competitor.Username),
            Volumes(image.Volumes, values),
            ports);
    }

    /// <summary>
    /// The image's own labels, rendered, with the platform's stamped over the top.
    /// </summary>
    /// <remarks>
    /// The platform's win a collision on purpose: they are what Close and Stop Marking find the containers
    /// by, and a session label an image happened to carry its own value for would strand its container.
    /// </remarks>
    private static Dictionary<string, string> Labels(
        SessionServicePlanRequest request,
        int index,
        IReadOnlyDictionary<ServicePlaceholder, string> values,
        string? competitorUsername)
    {
        var labels = Render(request.Session.DockerImages[index].Labels, values);

        labels[SessionServiceLabels.SessionKey] = request.Session.Slug;
        labels[SessionServiceLabels.ServiceKey] = SessionServiceNaming.ServiceNumber(index);

        if (competitorUsername is not null)
            labels[SessionServiceLabels.CompetitorKey] = competitorUsername;

        // Carries the slug rather than a bare "true" so Stop Marking can remove one session's marking
        // containers with the single label filter the daemon is asked for, and leave another session's alone.
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
