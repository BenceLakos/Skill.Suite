namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// A competitor's clone URL as THEIR machine reaches it, rewritten from the one the git host reported.
/// </summary>
/// <remarks>
/// The stored URL is whatever the git server calls itself, which in this deployment is a name only the
/// docker network resolves — the competitor would copy it, run <c>git clone</c>, and get a host-not-found.
/// The public name is derived the same way SQL Server's is, so the two cannot drift.
/// <para>
/// Only the scheme and the authority are replaced; the path is the git host's own and names the organisation
/// and the repository. The STORED value is never touched: the webhook resolves pushes through it and the
/// judgement runner clones with it, both from inside the docker network, where it is the correct one.
/// </para>
/// <para>
/// Plain <c>http</c>, like every other address a competitor is given: a venue LAN has no certificate
/// authority, and a self-signed certificate would have to be trusted on every competitor machine.
/// </para>
/// </remarks>
internal static class CompetitorGitUrl
{
    /// <summary>Hostname prefix the git host is published under, in place of the Suite's own.</summary>
    private const string GitPrefix = "git.";

    private const string Scheme = "http://";

    /// <summary>
    /// <paramref name="storedCloneUrl"/> pointed at the public git host, or unchanged when it cannot be.
    /// </summary>
    /// <remarks>
    /// Unchanged rather than blank whenever anything is missing — no request host, a host that is not a
    /// proxied <c>suite.</c> name, a stored value that is not an absolute URL. The stored URL is at least the
    /// one the server reported, which is right on a local stack and is something an expert can work with;
    /// a blank field is not.
    /// </remarks>
    public static string? For(string? storedCloneUrl, string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(storedCloneUrl))
            return storedCloneUrl;

        var host = CompetitorHostName.WithPrefix(requestHost, GitPrefix);
        if (host is null)
            return storedCloneUrl;

        return Uri.TryCreate(storedCloneUrl, UriKind.Absolute, out var url)
            ? Scheme + host + url.PathAndQuery
            : storedCloneUrl;
    }
}
