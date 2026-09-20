namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.StartMarking;
using Xunit;

/// <summary>
/// What starting a competitor's marking has to be told before anything is started.
/// </summary>
/// <remarks>
/// The marking machine's address goes verbatim into the proxy's <c>ClientIP</c> matcher, and Traefik does
/// not reject a rule it cannot parse — the router simply never matches. An address that is a typo would
/// therefore produce marking containers that are running, healthy and unreachable, discovered only when the
/// expert opens the URL.
/// </remarks>
public sealed class StartMarkingValidationTests
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly Guid Competitor = Guid.NewGuid();

    [Theory]
    [InlineData("10.0.0.42")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    public void AValidAddressIsAccepted(string address)
    {
        Assert.True(Validate(Session, Competitor, address).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingAddressIsRejected(string address)
    {
        // Nothing on the platform knows where an expert is sitting, so there is no default to fall back on.
        Assert.False(Validate(Session, Competitor, address).IsValid);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("10.0.0.256")]
    [InlineData("10.0.0.42:8080")]
    [InlineData("http://10.0.0.42")]
    public void AnAddressThatIsNotOneIsRejected(string address)
    {
        Assert.False(Validate(Session, Competitor, address).IsValid);
    }

    [Fact]
    public void TheSessionAndTheCompetitorAreBothRequired()
    {
        Assert.False(Validate(Guid.Empty, Competitor, "10.0.0.42").IsValid);
        Assert.False(Validate(Session, Guid.Empty, "10.0.0.42").IsValid);
    }

    private static FluentValidation.Results.ValidationResult Validate(
        Guid sessionId, Guid competitorId, string address) =>
        new StartMarkingValidator().Validate(new StartMarkingCommand(sessionId, competitorId, address));
}
