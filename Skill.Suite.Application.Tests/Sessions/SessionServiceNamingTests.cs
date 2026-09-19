namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions;
using Xunit;

/// <summary>
/// The container name and label values a session's services are started and later found by.
/// </summary>
/// <remarks>
/// Pinned because both ends of the session lifecycle depend on them agreeing. The name is how
/// <c>EnsureRunningAsync</c> decides a service is already up, so a name that changed between runs would start
/// a second copy of every service on each Start; the session label is the only thing Close removes by, so a
/// label that drifted from the slug would leave every container of the session running after the competition
/// ended.
/// </remarks>
public sealed class SessionServiceNamingTests
{
    [Fact]
    public void TheContainerNameCarriesTheSlugAndTheOneBasedPosition()
    {
        Assert.Equal("skill-suite-skill09-1", SessionServiceNaming.ContainerName("skill09", 0));
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "2")]
    [InlineData(9, "10")]
    public void ServicesAreNumberedFromOne(int index, string expected)
    {
        Assert.Equal(expected, SessionServiceNaming.ServiceNumber(index));
    }

    [Fact]
    public void TwoServicesOfOneSessionGetDifferentNames()
    {
        // Two services may legitimately run the same image with different ports, so the position — not the
        // image reference — is what has to separate them.
        Assert.NotEqual(
            SessionServiceNaming.ContainerName("skill09", 0),
            SessionServiceNaming.ContainerName("skill09", 1));
    }

    [Fact]
    public void TheSamePositionInTwoSessionsGetsDifferentNames()
    {
        Assert.NotEqual(
            SessionServiceNaming.ContainerName("skill09", 0),
            SessionServiceNaming.ContainerName("skill17", 0));
    }

    [Fact]
    public void TheNameIsStableAcrossCalls()
    {
        // Start is re-runnable, and the name is the only handle the daemon is asked about. A name derived
        // from anything per-call would silently start a second copy of the service on every retry.
        Assert.Equal(
            SessionServiceNaming.ContainerName("skill09", 2),
            SessionServiceNaming.ContainerName("skill09", 2));
    }

    [Fact]
    public void TheLabelKeysAreTheOnesRemovalFiltersOn()
    {
        Assert.Equal("skill-suite.session", SessionServiceLabels.SessionKey);
        Assert.Equal("skill-suite.service", SessionServiceLabels.ServiceKey);
    }
}
