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
    /// The session's shared database, then one access grant per competitor who holds a SQL login, counted.
    /// </summary>
    Databases,

    /// <summary>One long-running container per docker image configured on the session, counted.</summary>
    DockerServices,

    /// <summary>One access revocation per provisioned competitor repository, counted. Stopping only.</summary>
    RepositoryAccess,

    /// <summary>One stop per docker image configured on the session, counted. Stopping only.</summary>
    StoppingServices,
}
