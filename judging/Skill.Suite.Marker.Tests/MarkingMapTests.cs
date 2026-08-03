using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// Covers map loading, defaults, validation, and the part-routing rules the map configures.
/// </summary>
public sealed class MarkingMapTests
{
    [Fact]
    public void BareStringParts_BecomeSegmentRules()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts));

        Assert.Equal(["services", "program"], map.Parts.Select(part => part.Id));
        Assert.Equal(["services"], map.Parts[0].Segments);
    }

    [Fact]
    public void ObjectParts_SupportExplicitAndDottedSegments()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapObjectParts));

        // "AirWatch.Aqi.Services" expands into three segments; "Services" is already present so it is not
        // duplicated. A single string segment is accepted as well as an array.
        Assert.Equal(["AirWatch", "Aqi", "Services"], map.Parts[0].Segments);
        Assert.Equal(["Program"], map.Parts[1].Segments);
    }

    [Fact]
    public void OmittedScoring_FallsBackToTheDocumentedDefaults()
    {
        var scoring = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts)).Scoring;

        // These reproduce the previously hard-coded ramps exactly.
        Assert.Equal(40.0, scoring.CoverageFloor);
        Assert.Equal(80.0, scoring.CoverageCeil);
        Assert.Equal(30.0, scoring.MutationFloor);
        Assert.Equal(80.0, scoring.MutationCeil);
        Assert.Equal(0.30, scoring.CoverageWeight);
        Assert.Equal(0.70, scoring.CoreWeight);
    }

    [Fact]
    public void ExplicitScoring_IsRead()
    {
        var scoring = MarkingMapLoader.Load(Fixture.Path(Fixture.MapOverallOnly)).Scoring;

        Assert.Equal(50.0, scoring.CoverageFloor);
        Assert.Equal(90.0, scoring.CoverageCeil);
        Assert.Equal(0.5, scoring.CoverageWeight);
    }

    [Fact]
    public void NoParts_MeansOverallOnly()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapOverallOnly));

        Assert.Equal([MarkingMap.OverallPart], map.ScoredParts());
    }

    [Fact]
    public void ScoredParts_AppendsOverallAfterTheDeclaredParts()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts));

        Assert.Equal(["services", "program", "overall"], map.ScoredParts());
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreTolerated()
    {
        // The map is hand-edited per session, so it is read with comments enabled - the checked-in fixture
        // and the spec's own example both use them.
        Assert.NotEmpty(MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts)).Parts);
    }

    [Fact]
    public void AMissingMap_IsReported()
    {
        var ex = Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load("/nowhere/marking-map.json"));

        Assert.Contains("not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedJson_IsReported()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("map.json", "{\"parts\": [");

        var ex = Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(path));

        Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsupportedProtocol_IsRejected()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("map.json", """{"protocol": 2}""");

        var ex = Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(path));

        Assert.Contains("protocol 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvertedCoverageRamp_IsRejected()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("map.json", """{"scoring":{"coverageFloor":80,"coverageCeil":40}}""");

        var ex = Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(path));

        Assert.Contains("coverageCeil", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReservedOverallPart_IsRejected()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("map.json", """{"parts":["overall"]}""");

        var ex = Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(path));

        Assert.Contains("reserved", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicatePartsAndAspects_AreRejected()
    {
        using var workspace = new TempWorkspace();
        var parts = workspace.WriteFile("parts.json", """{"parts":["a","A"]}""");
        var aspects = workspace.WriteFile("aspects.json", """{"aspects":[{"id":"A1.1"},{"id":"A1.1"}]}""");

        Assert.Contains("more than once", Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(parts)).Message, StringComparison.Ordinal);
        Assert.Contains("more than once", Assert.Throws<MarkingMapException>(() => MarkingMapLoader.Load(aspects)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Acme.Services.WidgetTests", "services")]
    [InlineData("acme.services.widgettests", "services")]
    [InlineData("src/Services/Widget.cs", "services")]
    [InlineData("C:\\build\\src\\Program\\Main.cs", "program")]
    [InlineData("Acme.Other.MiscTests", null)]
    [InlineData("Acme.DisbursementServicesTests", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Routing_MatchesWholeSegmentsOnly(string? qualifiedName, string? expected)
    {
        // Whole-segment matching is deliberate: substring matching claimed types like
        // DisbursementServicesTests for the "services" part, which silently mis-scored them.
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts));

        Assert.Equal(expected, new PartRouter(map.Parts).Route(qualifiedName));
    }

    [Fact]
    public void Routing_WithNoDeclaredParts_AlwaysReturnsNull()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapOverallOnly));

        Assert.Null(new PartRouter(map.Parts).Route("Acme.Services.WidgetTests"));
    }
}
