namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Isolation and resource caps applied to a judgement container.
/// </summary>
/// <param name="IsolateNetwork">
/// Run with <c>--network none</c>. Safe to leave on: the image pull happens before the container's network
/// namespace is created, so private-registry pulls still work. With it off, a submission's test code has
/// unrestricted outbound access — enough to exfiltrate the hidden test suite it is being judged against, or
/// to fetch a package the offline restore was meant to deny.
/// </param>
/// <param name="Memory">Value for <c>--memory</c>, e.g. <c>2g</c>. Null leaves it uncapped.</param>
/// <param name="Cpus">Value for <c>--cpus</c>, e.g. <c>2</c>. Null leaves it uncapped.</param>
/// <param name="PidsLimit">Value for <c>--pids-limit</c>. Null leaves it uncapped.</param>
/// <remarks>
/// The resource caps default to unset on purpose. A black-box session runs mutation testing, which is
/// memory-hungry and long-running, and an arbitrary default would strangle it in a way that looks like a
/// competitor's bug rather than a configuration choice.
/// </remarks>
public sealed record ContainerLimits(
    bool IsolateNetwork = true,
    string? Memory = null,
    string? Cpus = null,
    int? PidsLimit = null);
