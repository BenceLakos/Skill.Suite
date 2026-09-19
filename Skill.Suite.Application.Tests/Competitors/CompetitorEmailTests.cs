namespace Skill.Suite.Application.Tests.Competitors;

using Skill.Suite.Application.Competitors.Accounts;
using Xunit;

/// <summary>
/// Composition of the synthetic address the git host insists every user has.
/// </summary>
public sealed class CompetitorEmailTests
{
    [Fact]
    public void TheUsernameAndDomainAreJoined() =>
        Assert.Equal("c01@competitors.local", CompetitorEmail.For("c01", "competitors.local"));

    [Fact]
    public void ALeadingAtOnTheDomainIsTolerated() =>
        // "@competitors.local" is the natural way to write a mail domain in configuration, and the double @ it
        // would otherwise produce is rejected by the host for every competitor at once.
        Assert.Equal("c01@competitors.local", CompetitorEmail.For("c01", "@competitors.local"));

    [Fact]
    public void TheResultIsLowerCased() =>
        // Addresses are compared case-insensitively by the host but stored as given; lower-casing keeps two
        // spellings of one competitor from looking like two accounts.
        Assert.Equal("c01@competitors.local", CompetitorEmail.For("C01", "Competitors.Local"));

    [Fact]
    public void SurroundingWhitespaceIsRemoved() =>
        Assert.Equal("c01@competitors.local", CompetitorEmail.For(" c01 ", " competitors.local "));
}
