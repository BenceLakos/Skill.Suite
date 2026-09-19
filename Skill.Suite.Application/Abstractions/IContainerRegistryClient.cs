namespace Skill.Suite.Application.Abstractions;

using Skill.Suite.Domain.Common;

/// <summary>
/// Reads what the container registry holds, so an operator can pick an image instead of typing one.
/// </summary>
/// <remarks>
/// The registry is the one the git host serves, which is why listing it needs an administrative credential:
/// competitor organisations are private, and an anonymous listing would show none of their packages.
/// <para>
/// A <see cref="Result{TValue}"/> rather than an exception because the only caller is a dropdown. The
/// session editor has to open — with the images already registered in the database — when the registry is
/// down, mid-restart, or reachable but refusing the stored credential.
/// </para>
/// </remarks>
public interface IContainerRegistryClient
{
    /// <summary>Lists every container package version visible to <paramref name="credential"/>.</summary>
    ValueTask<Result<IReadOnlyList<ContainerPackage>>> ListContainerPackagesAsync(
        BasicCredential credential, CancellationToken cancellationToken);
}
