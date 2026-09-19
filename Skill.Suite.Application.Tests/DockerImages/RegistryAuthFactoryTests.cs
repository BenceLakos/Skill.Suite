namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// Which docker pulls the session's registry credential is attached to.
/// </summary>
/// <remarks>
/// The bug this guards against is a session that cannot start at all. A session's services are images an
/// administrator chose and are routinely public, while the credential on the session belongs to the private
/// registry the judgement image lives in — so attaching it to <c>postgres:17</c> makes <c>docker login</c>
/// authenticate against Docker Hub with credentials Docker Hub has never heard of. That login failure takes
/// the whole service down with it, for an image that needed no credential in the first place.
/// </remarks>
public sealed class RegistryAuthFactoryTests
{
    private static readonly BasicCredential Credential = new("registry-user", "registry-secret");

    [Fact]
    public void AnImageNamingARegistryGetsTheCredential()
    {
        var auth = RegistryAuthFactory.ForHostedImage(Credential, "gitea:3000/skill09/judge:1.0");

        Assert.NotNull(auth);
        Assert.Equal("gitea:3000", auth.Server);
        Assert.Equal("registry-user", auth.Username);
        Assert.Equal("registry-secret", auth.Password);
    }

    [Theory]
    [InlineData("postgres:17")]
    [InlineData("library/redis:8")]
    public void APublicImageIsPulledAnonymouslyEvenWhenACredentialExists(string image)
    {
        Assert.Null(RegistryAuthFactory.ForHostedImage(Credential, image));
    }

    [Fact]
    public void NoCredentialMeansNoLogin()
    {
        Assert.Null(RegistryAuthFactory.ForHostedImage(credential: null, "gitea:3000/skill09/judge:1.0"));
    }

    [Fact]
    public void TheJudgementPullKeepsTheDaemonDefaultForAHostlessReference()
    {
        // The unconditional overload is what the judgement runner uses, and it must keep behaving as it did:
        // a null server lets the daemon fall back to its own configuration.
        var auth = RegistryAuthFactory.For(Credential, "skill-marker:latest");

        Assert.Null(auth.Server);
        Assert.Equal("registry-user", auth.Username);
    }
}
