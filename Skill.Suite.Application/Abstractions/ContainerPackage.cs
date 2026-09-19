namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// One container package version published on the registry, as the registry names it.
/// </summary>
/// <remarks>
/// Deliberately not an image reference. The registry host a docker daemon must pull from is a deployment
/// concern — the app container and the host daemon reach the same registry under different names — so the
/// caller resolves it and composes the reference, and this stays the registry's own view.
/// </remarks>
public sealed record ContainerPackage(string Owner, string Name, string Version);
