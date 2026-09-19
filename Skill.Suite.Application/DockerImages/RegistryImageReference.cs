namespace Skill.Suite.Application.DockerImages;

using Skill.Suite.Application.Abstractions;

/// <summary>
/// Turns a registry package into the reference a docker daemon can pull.
/// </summary>
public static class RegistryImageReference
{
    /// <summary>Marks a package version that is a manifest digest rather than a tag.</summary>
    private const string DigestVersionPrefix = "sha256:";

    /// <summary>
    /// The pullable reference for <paramref name="package"/>, or <see langword="null"/> when there is none to
    /// offer.
    /// </summary>
    /// <remarks>
    /// Untagged versions are skipped rather than rendered as digests. Every tagged image also appears in the
    /// listing under its digest, so offering both would double the dropdown with entries an operator cannot
    /// read — and a digest is not what anyone picks an image by.
    /// <para>
    /// Owner and name are lower-cased because registries reject uppercase path components, while a Gitea
    /// account name is free to contain them.
    /// </para>
    /// </remarks>
    public static string? Compose(string registryHost, ContainerPackage package)
    {
        if (string.IsNullOrWhiteSpace(registryHost)
            || string.IsNullOrWhiteSpace(package.Owner)
            || string.IsNullOrWhiteSpace(package.Name)
            || string.IsNullOrWhiteSpace(package.Version)
            || package.Version.StartsWith(DigestVersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var host = registryHost.Trim().Trim('/');
        return $"{host}/{package.Owner.ToLowerInvariant()}/{package.Name.ToLowerInvariant()}:{package.Version}";
    }
}
