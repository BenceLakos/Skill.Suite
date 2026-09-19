namespace Skill.Suite.Application.DockerImages;

using Skill.Suite.Application.Abstractions;

/// <summary>
/// Pairs a stored pull credential with the registry an image reference names.
/// </summary>
/// <remarks>
/// Shared by the judgement runner and session start so the two cannot drift: both pull with the session's
/// <c>JudgementImagePullCredentialId</c>, and a registry host derived differently on each side is a
/// <c>docker login</c> that succeeds in one place and fails in the other.
/// </remarks>
internal static class RegistryAuthFactory
{
    /// <summary>
    /// The credential against the registry the image names, or against the daemon's default when it names none.
    /// </summary>
    public static RegistryAuth For(BasicCredential credential, string image) =>
        new(RegistryHostExtractor.Extract(image), credential.Username, credential.Secret);

    /// <summary>
    /// The same, but only for an image whose reference actually names a registry host.
    /// </summary>
    /// <remarks>
    /// A session's services are images an administrator chose and are routinely public — <c>postgres:17</c>,
    /// <c>redis:8</c> — while the pull credential on the session exists for the private registry the judgement
    /// image lives in. Handing that credential to a reference with no host makes <c>docker login</c>
    /// authenticate against Docker Hub with credentials Docker Hub has never heard of, which fails and stops a
    /// perfectly public service from starting at all.
    /// </remarks>
    public static RegistryAuth? ForHostedImage(BasicCredential? credential, string image) =>
        credential is null || RegistryHostExtractor.Extract(image) is null
            ? null
            : For(credential, image);
}
