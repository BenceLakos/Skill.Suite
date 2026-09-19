namespace Skill.Suite.Application.DockerImages;

/// <summary>
/// Extracts the registry hostname out of a docker image reference so we can pass it to
/// <c>docker login</c>. For plain "user/image:tag" or "image:tag" references the host is
/// the implicit docker.io — we return null and let the daemon fall back.
/// </summary>
public static class RegistryHostExtractor
{
    public static string? Extract(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
            return null;

        var firstSlash = imageReference.IndexOf('/');
        if (firstSlash < 0)
            return null;

        var firstSegment = imageReference[..firstSlash];

        // A registry host always contains a dot or colon (port) or is the literal "localhost".
        // Otherwise it's a docker.io user namespace like "library" or "gitea".
        return firstSegment.Contains('.') || firstSegment.Contains(':') ||
               firstSegment.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            ? firstSegment
            : null;
    }
}
