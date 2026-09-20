namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Xunit;

/// <summary>
/// The catalogue itself: every value named once, every value scoped, and nothing named twice.
/// </summary>
/// <remarks>
/// Pinned because three unrelated things read this table and all three have to agree — the validators refuse
/// a name that is not in it, the planner resolves the ones that are, and the session form lists them for the
/// administrator. A placeholder added to the enum and forgotten here would be offered on the form and
/// throw when a session using it was started.
/// </remarks>
public sealed class ServicePlaceholdersTests
{
    [Fact]
    public void EveryPlaceholderInTheEnumIsInTheCatalogue()
    {
        var declared = Enum.GetValues<ServicePlaceholder>();

        Assert.Equal(declared.Length, ServicePlaceholders.All.Count);

        foreach (var placeholder in declared)
        {
            Assert.False(string.IsNullOrWhiteSpace(ServicePlaceholders.NameOf(placeholder)));

            // Throws rather than defaulting if the scope table is missing one, which is the point: a
            // placeholder with no scope would silently be read as session-scoped and leave a per-competitor
            // service running as a single shared container.
            Assert.True(Enum.IsDefined(ServicePlaceholders.ScopeOf(placeholder)));
        }
    }

    [Fact]
    public void NoTwoPlaceholdersShareAName()
    {
        var names = ServicePlaceholders.All.Select(ServicePlaceholders.NameOf).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryNameParsesBackToItsOwnPlaceholder()
    {
        foreach (var placeholder in ServicePlaceholders.All)
        {
            Assert.True(ServicePlaceholders.TryParse(ServicePlaceholders.NameOf(placeholder), out var parsed));
            Assert.Equal(placeholder, parsed);
        }
    }

    [Fact]
    public void NamesAreMatchedWithoutRegardToCase()
    {
        // A session author typing {{Competitor.Username}} has made no mistake worth refusing a save over.
        Assert.True(ServicePlaceholders.TryParse("COMPETITOR.USERNAME", out var parsed));
        Assert.Equal(ServicePlaceholder.CompetitorUsername, parsed);
    }

    [Fact]
    public void AnUncataloguedNameDoesNotParse()
    {
        Assert.False(ServicePlaceholders.TryParse("database.sever", out _));
    }

    [Fact]
    public void TheTokenIsTheNameInBraces()
    {
        Assert.Equal("{{competitor.username}}", ServicePlaceholders.TokenOf(ServicePlaceholder.CompetitorUsername));
    }

    [Theory]
    [InlineData(ServicePlaceholder.SessionName, ServicePlaceholderScope.Session)]
    [InlineData(ServicePlaceholder.SessionSlug, ServicePlaceholderScope.Session)]
    [InlineData(ServicePlaceholder.CompetitorUsername, ServicePlaceholderScope.Competitor)]
    [InlineData(ServicePlaceholder.CompetitorFullName, ServicePlaceholderScope.Competitor)]
    [InlineData(ServicePlaceholder.CompetitorIpAddress, ServicePlaceholderScope.Competitor)]
    [InlineData(ServicePlaceholder.CompetitorCountryCode, ServicePlaceholderScope.Competitor)]
    [InlineData(ServicePlaceholder.CompetitorPassword, ServicePlaceholderScope.Competitor)]
    [InlineData(ServicePlaceholder.DatabaseName, ServicePlaceholderScope.Database)]
    [InlineData(ServicePlaceholder.DatabaseServer, ServicePlaceholderScope.Database)]
    [InlineData(ServicePlaceholder.DatabaseLogin, ServicePlaceholderScope.Database)]
    [InlineData(ServicePlaceholder.DatabasePassword, ServicePlaceholderScope.Database)]
    [InlineData(ServicePlaceholder.DatabaseConnectionString, ServicePlaceholderScope.Database)]
    public void TheScopeIsWhatDecidesHowManyContainersAServiceBecomes(
        ServicePlaceholder placeholder, ServicePlaceholderScope expected)
    {
        Assert.Equal(expected, ServicePlaceholders.ScopeOf(placeholder));
    }
}
