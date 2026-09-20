namespace Skill.Suite.Application.Tests.Competitors;

using Skill.Suite.Application.Competitors.CreateCompetitor;
using Skill.Suite.Application.Competitors.UpdateCompetitor;
using Xunit;

/// <summary>
/// What a competitor's optional second device has to be.
/// </summary>
/// <remarks>
/// Asserted against both validators, because a competitor edited into a state they could not have been
/// created in is the same broken row either way — and this one is felt at the proxy: the address goes
/// verbatim into a <c>ClientIP</c> matcher, and Traefik answers an unparseable rule by never matching rather
/// than by complaining.
/// </remarks>
public sealed class CompetitorMobileIpValidationTests
{
    private const string Workstation = "10.0.0.7";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoMobileDeviceIsFine(string? mobile)
    {
        // Most competitions hand none out, so absent has to be the easy, valid answer.
        Assert.True(Create(mobile).IsValid);
        Assert.True(Update(mobile).IsValid);
    }

    [Theory]
    [InlineData("10.0.0.99")]
    [InlineData("192.168.1.50")]
    [InlineData("2001:db8::1")]
    public void AValidAddressThatIsNotTheWorkstationIsAccepted(string mobile)
    {
        Assert.True(Create(mobile).IsValid);
        Assert.True(Update(mobile).IsValid);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("10.0.0.256")]
    [InlineData("10.0.0.99:8080")]
    public void AnAddressThatIsNotOneIsRejected(string mobile)
    {
        Assert.False(Create(mobile).IsValid);
        Assert.False(Update(mobile).IsValid);
    }

    [Fact]
    public void TheWorkstationAddressAgainIsRejected()
    {
        // Two names for one machine buy nothing and read as a mistake — and would emit the same ClientIP
        // matcher twice in the proxy rule.
        Assert.False(Create(Workstation).IsValid);
        Assert.False(Update(Workstation).IsValid);
    }

    [Fact]
    public void TheComparisonIgnoresSurroundingWhitespace()
    {
        Assert.False(Create("  " + Workstation + "  ").IsValid);
    }

    private static FluentValidation.Results.ValidationResult Create(string? mobile) =>
        new CreateCompetitorValidator().Validate(new CreateCompetitorCommand(
            "c01", "Joe Doe", "correct-horse", Workstation, mobile, "HUN"));

    private static FluentValidation.Results.ValidationResult Update(string? mobile) =>
        new UpdateCompetitorValidator().Validate(new UpdateCompetitorCommand(
            Guid.NewGuid(), "c01", "Joe Doe", "correct-horse", Workstation, mobile, "HUN"));
}
