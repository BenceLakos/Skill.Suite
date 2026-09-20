namespace Skill.Suite.Application.DockerImages;

/// <summary>
/// Restates a stored image reference in terms the docker daemon can resolve.
/// </summary>
/// <remarks>
/// References are stored, not derived: they were saved before the daemon view of the registry existed, and an
/// operator registering an image pastes the process-view host out of habit because that is the host the
/// application's own URLs carry. Either way the daemon — the host daemon, over the mounted socket — is handed a
/// compose service name it cannot look up, and the pull fails before it starts. Rewriting happens on the way
/// out only; the stored reference is left alone so what an administrator typed stays visible for traceability.
/// </remarks>
public static class DaemonImageReference
{
    /// <summary>
    /// <paramref name="image"/> with the process-view registry host swapped for the daemon-facing one, or
    /// unchanged when its host is not the platform's own registry — including a reference that names no host at
    /// all, such as a public <c>postgres:17</c>.
    /// </summary>
    public static string ForDaemon(string image, string? gitInternalBaseUrl)
    {
        var processHost = RegistryHostResolver.ResolveProcessHost(gitInternalBaseUrl);
        if (processHost is null)
            return image;

        var referenceHost = RegistryHostExtractor.Extract(image);
        if (referenceHost is null || !referenceHost.Equals(processHost, StringComparison.OrdinalIgnoreCase))
            return image;

        return RegistryHostResolver.Resolve(gitInternalBaseUrl) + image[referenceHost.Length..];
    }
}
