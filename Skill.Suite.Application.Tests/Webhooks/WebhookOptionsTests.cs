using Skill.Suite.Application.Webhooks;
using Xunit;

namespace Skill.Suite.Application.Tests.Webhooks;

/// <summary>
/// Defaults on <see cref="WebhookOptions"/> that are security decisions rather than preferences.
/// </summary>
public sealed class WebhookOptionsTests
{
    [Fact]
    public void UnsignedPushesAreRejectedByDefault() =>
        // The endpoint is anonymous by necessity — a git host cannot hold an application session — so the HMAC
        // is the only authentication. A default of true made it an open judging trigger: anyone reaching the
        // port could create runs, supersede a competitor's in-flight submission, and make the server clone an
        // arbitrary repository with the stored credential. Only the local harness should ever opt in.
        Assert.False(new WebhookOptions().AllowUnsignedPushes);

    [Fact]
    public void JudgeNetworkIsIsolatedByDefault() =>
        // Competitor test code runs in that container. The image pull happens before the network namespace
        // exists, so private registries still work with this on.
        Assert.True(new WebhookOptions().IsolateJudgeNetwork);
}
