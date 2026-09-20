namespace Skill.Suite.Application.Sessions;

/// <summary>
/// Which part of starting or stopping a session a progress report is about.
/// </summary>
/// <remarks>
/// The stage is what the caller turns into a label, so the values name work the admin can recognise rather
/// than handler methods. A new stage is one value here plus one report call in the handler — the UI needs no
/// structural change to show it.
/// <para>
/// Shared by both commands rather than one enum each: the sessions page draws a single progress panel, and
/// two parallel enums would mean two of everything behind it for no gain.
/// </para>
/// </remarks>
public enum SessionProvisioningStage
{
    /// <summary>
    /// The organisation, the template repository and the starter package push — one-off work whose size is
    /// not known in advance, so it is reported without a count.
    /// </summary>
    Preparing,

    /// <summary>One repository per competitor, counted.</summary>
    Repositories,

    /// <summary>
    /// One database per competitor who holds a SQL login — created, seeded and granted — counted.
    /// </summary>
    Databases,

    /// <summary>
    /// One long-running container per docker service the session resolves to, counted.
    /// </summary>
    /// <remarks>
    /// Containers rather than images, because a service whose configuration names a competitor is started
    /// once per competitor: counting images would leave the bar at one of three while twenty containers came
    /// up, which is the "is it stuck?" question the report exists to answer.
    /// </remarks>
    DockerServices,

    /// <summary>
    /// One marking container per service the closed session resolves to, counted. Marking only.
    /// </summary>
    MarkingServices,

    /// <summary>One access revocation per provisioned competitor repository, counted. Stopping only.</summary>
    RepositoryAccess,

    /// <summary>One stop per docker image configured on the session, counted. Stopping only.</summary>
    StoppingServices,
}
