namespace Skill.Suite.Application.Tests.Competitors;

using System.Net;
using Skill.Suite.Application.Competitors;
using Xunit;

/// <summary>
/// How the address a request arrived from is turned into "this is competitor N's desk".
/// </summary>
/// <remarks>
/// Worth asserting because every failure here is silent. A comparison that misses simply leaves the sign-in
/// page as it was, which looks exactly like a competitor whose address nobody recorded — and the one case that
/// is not silent, two competitors at one address, must hand out nobody's password rather than the first
/// one's.
/// </remarks>
public sealed class WorkstationMatchTests
{
    [Fact]
    public void AnExactAddressMatches()
    {
        Assert.True(WorkstationMatch.IsSameWorkstation(IPAddress.Parse("10.0.0.5"), "10.0.0.5"));
    }

    [Fact]
    public void AnIPv4ClientOnADualStackSocketMatchesTheIPv4Row()
    {
        // This is what Kestrel hands over for an IPv4 client on a dual-stack listener, and comparing the
        // strings would have missed every competitor in the room.
        var client = IPAddress.Parse("::ffff:10.0.0.5");

        Assert.True(WorkstationMatch.IsSameWorkstation(client, "10.0.0.5"));
    }

    [Fact]
    public void WhitespaceAroundTheStoredAddressIsIgnored()
    {
        Assert.True(WorkstationMatch.IsSameWorkstation(IPAddress.Parse("10.0.0.5"), "  10.0.0.5 "));
    }

    [Fact]
    public void TheSameIPv6AddressWrittenTwoWaysMatches()
    {
        var client = IPAddress.Parse("2001:db8::1");

        Assert.True(WorkstationMatch.IsSameWorkstation(client, "2001:0db8:0000:0000:0000:0000:0000:0001"));
    }

    [Fact]
    public void AScopeIdIsNotPartOfTheWorkstation()
    {
        // The scope names the interface the address was seen on, not the host.
        var client = IPAddress.Parse("fe80::1%3");

        Assert.True(WorkstationMatch.IsSameWorkstation(client, "fe80::1"));
    }

    [Fact]
    public void LoopbackIsTheSameWorkstationAcrossBothFamilies()
    {
        // A browser on the machine running the stack arrives as ::1 while the competitor row says 127.0.0.1.
        Assert.True(WorkstationMatch.IsSameWorkstation(IPAddress.IPv6Loopback, "127.0.0.1"));
        Assert.True(WorkstationMatch.IsSameWorkstation(IPAddress.Loopback, "::1"));
    }

    [Fact]
    public void ADifferentAddressDoesNotMatch()
    {
        Assert.False(WorkstationMatch.IsSameWorkstation(IPAddress.Parse("10.0.0.5"), "10.0.0.6"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    [InlineData("10.0.0.5:443")]
    [InlineData(null)]
    public void AnUnusableStoredValueNeverMatches(string? stored)
    {
        Assert.False(WorkstationMatch.IsSameWorkstation(IPAddress.Parse("10.0.0.5"), stored));
    }

    [Fact]
    public void NoClientAddressNeverMatches()
    {
        Assert.False(WorkstationMatch.IsSameWorkstation(null, "10.0.0.5"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("nonsense")]
    public void ParseRefusesWhatIsNotAnAddress(string? value)
    {
        Assert.Null(WorkstationMatch.Parse(value));
    }

    [Fact]
    public void ParseCanonicalisesAnIPv4MappedAddress()
    {
        Assert.Equal(IPAddress.Parse("10.0.0.5"), WorkstationMatch.Parse("::ffff:10.0.0.5"));
    }

    [Fact]
    public void OneCompetitorAtTheAddressIsFoundByPosition()
    {
        var stored = new[] { "10.0.0.4", "10.0.0.5", "10.0.0.6" };

        var matches = WorkstationMatch.MatchIndexes(IPAddress.Parse("10.0.0.5"), stored);

        Assert.Equal([1], matches);
    }

    [Fact]
    public void NobodyAtTheAddressIsNoMatch()
    {
        var stored = new[] { "10.0.0.4", "10.0.0.6" };

        Assert.Empty(WorkstationMatch.MatchIndexes(IPAddress.Parse("10.0.0.5"), stored));
    }

    [Fact]
    public void EveryCompetitorSharingAnAddressIsReported()
    {
        // The caller has to be able to tell "nobody" from "more than one": the second is a data-entry mistake
        // that must produce no pre-fill rather than one of the two competitors' passwords.
        var stored = new[] { "10.0.0.5", "10.0.0.6", "::ffff:10.0.0.5" };

        var matches = WorkstationMatch.MatchIndexes(IPAddress.Parse("10.0.0.5"), stored);

        Assert.Equal([0, 2], matches);
    }
}
