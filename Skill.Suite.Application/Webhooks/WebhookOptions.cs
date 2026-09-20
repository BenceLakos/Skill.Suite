namespace Skill.Suite.Application.Webhooks;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhook";

    /// <summary>Path inside the app process where cloned submissions are written.</summary>
    public string WorkingDirectory { get; set; } = "/workdir";

    /// <summary>
    /// Name of the docker volume backing <see cref="WorkingDirectory"/>. The judgement
    /// container the app spawns mounts a subpath of this same volume, so the deployment
    /// stays free of host filesystem paths.
    /// </summary>
    public string WorkdirVolumeName { get; set; } = "skill-suite-workdir";

    /// <summary>
    /// Docker network a session service joins when it is given a domain to be routed on.
    /// </summary>
    /// <remarks>
    /// <b>Must match <c>--providers.docker.network</c> on the Traefik container</b>, which is
    /// <c>skill-suite</c> in <c>infra/compose.yaml</c>. Traefik reads a container's labels off the daemon
    /// wherever it is, but it can only forward to an address it can reach, so a routed container that is not
    /// on this network is discovered, routed, and answers every request with a gateway error.
    /// <para>
    /// A setting rather than a derivation, unlike the registry host and the SQL Server address: nothing this
    /// application already knows carries the network name. A service with no domain keeps today's behaviour
    /// and joins nothing, so this is only read for services that are routed.
    /// </para>
    /// </remarks>
    public string ServiceNetwork { get; set; } = "skill-suite";

    /// <summary>Container path the cloned source is mounted at inside the judgement image.</summary>
    public string ContainerWorkdir { get; set; } = "/workspace";

    /// <summary>
    /// Template for the per-submission folder. Tokens: {random}, {competitor}, {commit}, {timestamp}.
    /// </summary>
    public string FolderTemplate { get; set; } = "submission-{competitor}-{random}";

    /// <summary>
    /// Internal base URL (scheme + host + optional port) used to clone competitor
    /// submissions from inside the docker network. When set, it replaces the
    /// scheme/host/port of whatever the webhook advertised — typically a public URL
    /// like <c>http://localhost:3000</c> the app can't reach — while the path and
    /// query of the original URL are preserved. The original URL is kept on the
    /// TestRun for traceability.
    /// <para>
    /// The container-registry host the UI offers, and pulls with, is derived from this same setting as the
    /// docker daemon sees it — <c>localhost:&lt;port&gt;</c> when this names a compose service, because the
    /// daemon runs on the host and cannot resolve docker-network names.
    /// </para>
    /// </summary>
    public string? GitInternalBaseUrl { get; set; }

    /// <summary>
    /// Writable directory inside the judgement container where it should write its
    /// JSON-lines event log. The app mounts a per-submission writable subpath at this
    /// location and reads <c>events.jsonl</c> from it after the container exits. The
    /// <c>LOG_DIRECTORY</c> environment variable is also set to this path so the judge
    /// can discover it without hard-coding.
    /// </summary>
    public string ContainerLogDirectory { get; set; } = "/var/log/skill-suite";

    /// <summary>File name the judgement image writes inside <see cref="ContainerLogDirectory"/>.</summary>
    public string EventLogFileName { get; set; } = "events.jsonl";

    /// <summary>
    /// Wall-clock budget for one judgement run: clone, image pull and container combined.
    /// </summary>
    /// <remarks>
    /// A backstop, not the primary limit — a well-behaved judge image enforces its own, finer-grained
    /// per-step budgets. This one exists because those cannot cover everything: a slow <c>docker pull</c>
    /// from a private registry, a hanging <c>git clone</c>, or a third-party image that does not use the
    /// judge shell library at all. Since the worker processes runs serially, one unbounded run stalls every
    /// other competitor's submission, so keep this comfortably above the image's own budget and let the
    /// image report a timeout first when it can.
    /// </remarks>
    public TimeSpan RunTimeout { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>Run judgement containers with <c>--network none</c>.</summary>
    /// <remarks>
    /// On by default. The image pull happens before the container's network namespace exists, so private
    /// registries still work. Turning it off gives a competitor's test code outbound access to the internet.
    /// </remarks>
    public bool IsolateJudgeNetwork { get; set; } = true;

    /// <summary>Optional <c>--memory</c> cap for judgement containers, e.g. <c>2g</c>.</summary>
    public string? JudgeMemoryLimit { get; set; }

    /// <summary>Optional <c>--cpus</c> cap for judgement containers, e.g. <c>2</c>.</summary>
    public string? JudgeCpuLimit { get; set; }

    /// <summary>Optional <c>--pids-limit</c> for judgement containers.</summary>
    public int? JudgePidsLimit { get; set; }

    /// <summary>
    /// URL the git host should POST push events to, written into the webhook at session start.
    /// </summary>
    /// <remarks>
    /// This is the git host's view of this application, not the browser's: the host reaches it over the
    /// container network, so the default names the compose service rather than <c>localhost</c>. Getting it
    /// wrong is silent — repositories are provisioned, the hook is installed, and no push ever arrives.
    /// </remarks>
    public string PublicWebhookUrl { get; set; } = "http://skill-suite:8080/webhooks/git";

    /// <summary>
    /// Accept a push whose session carries no webhook secret. <b>Off by default; never turn it on in
    /// production.</b>
    /// </summary>
    /// <remarks>
    /// A session started through the UI always gets a secret, so the only sessions this affects are ones
    /// inserted straight into the database — which is exactly what the scripted end-to-end harness does,
    /// because a secret is DataProtection-encrypted and only the running application can produce one.
    /// <para>
    /// It exists as an explicit switch rather than an implicit fallback because the implicit version was a hole:
    /// with no secret the endpoint accepted anonymous pushes, so anyone who could reach the port could create
    /// runs, supersede a competitor's in-flight submission, and make the server clone an arbitrary repository
    /// with the stored credential. Failing closed is the only safe default; the harness opts in deliberately.
    /// </para>
    /// </remarks>
    public bool AllowUnsignedPushes { get; set; }

    /// <summary>How many judgement runs may execute at once.</summary>
    /// <remarks>
    /// <para>
    /// Deliberately conservative by default: judgement containers build and run a test suite, and the host is
    /// usually also running the database, the git server and this application. Raise it for a real competition
    /// once you know what the machine can take.
    /// </para>
    /// <para>
    /// No per-competitor fairness is layered on top. A new push supersedes that competitor's older in-flight
    /// runs, so only one run per competitor is ever active and the slots spread across competitors naturally.
    /// </para>
    /// </remarks>
    public int MaxConcurrentRuns { get; set; } = 2;
}
