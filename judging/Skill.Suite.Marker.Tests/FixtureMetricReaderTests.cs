using Skill.Suite.Marker.Readers;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// The two readers behind the per-test-class measurements.
/// </summary>
/// <remarks>
/// Both answer a question the rollup cannot: not "how good is this suite" but "which class in it did the
/// work". The fixtures are sized so every expected number can be counted by hand from the file.
/// </remarks>
public sealed class FixtureMetricReaderTests
{
    [Fact]
    public void FixtureCoverage_SumsTheWholeReportPerClass()
    {
        var coverage = FixtureCoverageReader.Read(Fixture.Path(Fixture.FixtureCoverage));

        // Both runs measure the same eight coverable lines; only the hits differ. Equal denominators are what
        // makes two classes' coverage comparable at all.
        Assert.Equal(6, coverage["WidgetTests"].LinesCovered);
        Assert.Equal(8, coverage["WidgetTests"].LinesTotal);
        Assert.Equal(2, coverage["SmokeTests"].LinesCovered);
        Assert.Equal(8, coverage["SmokeTests"].LinesTotal);
    }

    [Fact]
    public void FixtureCoverage_CountsLinesTheSameWayTheRollupDoes()
    {
        // The acceptance oracle CoberturaReader itself uses: direct-child <line> elements only, which is what
        // makes the totals equal the report's own lines-covered / lines-valid attributes. A recursive read
        // would double WidgetTests, whose first class also lists its lines under a <method>.
        var coverage = FixtureCoverageReader.Read(Fixture.Path(Fixture.FixtureCoverage));

        Assert.Equal(0.75, coverage["WidgetTests"].Rate, 4);
        Assert.Equal(0.25, coverage["SmokeTests"].Rate, 4);
    }

    [Fact]
    public void FixtureCoverage_WithNoReport_YieldsNoEntryRatherThanAZero()
    {
        var coverage = FixtureCoverageReader.Read(Fixture.Path(Fixture.FixtureCoverage));

        // MiscTests has a directory but no report: its run did not happen. Zero would say it covered nothing.
        Assert.False(coverage.ContainsKey("MiscTests"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/no/such/directory")]
    public void FixtureCoverage_WithoutADirectory_IsEmptyRatherThanFatal(string? directory) =>
        Assert.Empty(FixtureCoverageReader.Read(directory));

    [Fact]
    public void FixtureMutation_AttributesEachMutantToTheClassesThatReachedIt()
    {
        var mutation = StrykerReader.ReadByFixture(Fixture.Path(Fixture.StrykerPerTest));

        Assert.True(mutation.PerTest);

        var widget = mutation.ByFixture["WidgetTests"];
        Assert.Equal(1, widget.Killed);
        Assert.Equal(1, widget.Survived);
        Assert.Equal(1, widget.Timeout);
        Assert.Equal(3, widget.Covered);

        // A timeout counts as a detection, exactly as it does in the rollup.
        Assert.Equal(2.0 / 3.0, widget.KillRate, 4);
    }

    [Fact]
    public void FixtureMutation_DoesNotCreditAClassForAMutantAnotherClassKilled()
    {
        // The reason bail has to be off. Both classes reach m1; only WidgetTests detects it, so SmokeTests
        // must carry that miss.
        var mutation = StrykerReader.ReadByFixture(Fixture.Path(Fixture.StrykerPerTest));

        var smoke = mutation.ByFixture["SmokeTests"];
        Assert.Equal(1, smoke.Killed);
        Assert.Equal(1, smoke.Survived);
        Assert.Equal(1, smoke.Timeout);
    }

    [Fact]
    public void FixtureMutation_CountsOnlyWhatAClassReached()
    {
        var mutation = StrykerReader.ReadByFixture(Fixture.Path(Fixture.StrykerPerTest));

        // Seven mutants in the report, one of them reached by nothing and one a compile error. MiscTests
        // touched exactly one, and is measured on that one rather than on the whole population.
        var misc = mutation.ByFixture["MiscTests"];
        Assert.Equal(1, misc.Total);
        Assert.Equal(1, misc.Covered);
        Assert.Equal(0, misc.NoCoverage);
        Assert.Equal(1.0, misc.KillRate);
    }

    [Fact]
    public void FixtureMutation_FallsBackToTheTestFileWhenTheNameCarriesNoClass()
    {
        // MiscTests' only test reports a bare display name. The file it was found in is the one remaining
        // piece of evidence, and dropping the test instead would silently lose a whole class.
        var mutation = StrykerReader.ReadByFixture(Fixture.Path(Fixture.StrykerPerTest));

        Assert.Contains("MiscTests", mutation.ByFixture.Keys);
    }

    [Fact]
    public void FixtureMutation_WithoutPerTestData_SaysSoRatherThanReportingZeroes()
    {
        // The pre-perTest report shape: statuses, no testFiles, no coveredBy. Every class would read 0 killed.
        var mutation = StrykerReader.ReadByFixture(Fixture.Path(Fixture.Stryker));

        Assert.False(mutation.PerTest);
        Assert.Empty(mutation.ByFixture);
    }

    [Fact]
    public void FixtureMutation_WithoutAReport_IsUnavailableRatherThanFatal()
    {
        Assert.False(StrykerReader.ReadByFixture(null).PerTest);
        Assert.False(StrykerReader.ReadByFixture("/no/such/report.json").PerTest);
    }

    [Fact]
    public void TrxFixtureNames_AreTheSimpleClassNamesTheStreamUses()
    {
        var fixtures = TrxReader.ReadFixtureNames([Fixture.Path(Fixture.Trx)]);

        Assert.Equal(["MiscTests", "SmokeTests", "WidgetTests"], fixtures.OrderBy(f => f, StringComparer.Ordinal));
    }

    [Fact]
    public void EventFixtureNames_ComeFromTheHarnessesOwnNaming()
    {
        var fixtures = EventReplay.ReadFixtureNames(Fixture.Path(Fixture.EventsWithAspects));

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, name => Assert.DoesNotContain('.', name));
    }

    [Fact]
    public void EventFixtureNames_WithNoStream_AreEmptyRatherThanFatal() =>
        Assert.Empty(EventReplay.ReadFixtureNames("/no/such/events.jsonl"));
}
