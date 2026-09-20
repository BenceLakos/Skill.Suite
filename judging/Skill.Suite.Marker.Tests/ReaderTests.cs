using System.Xml.Linq;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Readers;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// Covers the three report readers against real and hand-built fixtures.
/// </summary>
public sealed class ReaderTests
{
    private static readonly PartRouter TwoParts = new(
        [PartRule.FromId("services").Normalized(), PartRule.FromId("program").Normalized()]);

    // ---------- Cobertura ----------

    [Fact]
    public void Cobertura_TotalsMatchTheReportsOwnRootAttributes()
    {
        // The oracle: a Cobertura report states its own lines-valid / lines-covered, so a correct reader
        // must reproduce them. This is a real coverlet report.
        var root = XDocument.Load(Fixture.Path(Fixture.Cobertura)).Root!;
        var declaredTotal = int.Parse(root.Attribute("lines-valid")!.Value);
        var declaredCovered = int.Parse(root.Attribute("lines-covered")!.Value);

        var coverage = CoberturaReader.Read([Fixture.Path(Fixture.Cobertura)], TwoParts);
        var overall = coverage[MarkingMap.OverallPart];

        Assert.Equal(declaredTotal, overall.LinesTotal);
        Assert.Equal(declaredCovered, overall.LinesCovered);
        Assert.Equal(300, overall.LinesTotal);
        Assert.Equal(269, overall.LinesCovered);
    }

    [Fact]
    public void Cobertura_DoesNotDoubleCountLines()
    {
        // Coverlet emits every line twice - once under its method, once at class level. Reading
        // descendants recursively inflates both numbers to 600/538 while leaving the rate intact, so the
        // bug hid behind a plausible-looking percentage.
        var coverage = CoberturaReader.Read([Fixture.Path(Fixture.Cobertura)], TwoParts);
        var overall = coverage[MarkingMap.OverallPart];

        Assert.NotEqual(600, overall.LinesTotal);
        Assert.NotEqual(538, overall.LinesCovered);
    }

    [Fact]
    public void Cobertura_RoutesFilesToPartsByPathSegment()
    {
        var coverage = CoberturaReader.Read([Fixture.Path(Fixture.Cobertura)], TwoParts);

        // Services/DefaultDiagnosticDataProcessor.cs -> services, by folder.
        Assert.Equal(new CoverageStats(269, 289), coverage["services"]);

        // Program.cs -> program: paths are split on '.' as well as the separators, so a file's own stem is
        // a segment too. That is deliberate - it lets a part be named after a single file - but it does mean
        // a part called "services" would also claim a file literally named Services.cs.
        Assert.Equal(new CoverageStats(0, 11), coverage["program"]);

        Assert.Equal(new CoverageStats(269, 300), coverage[MarkingMap.OverallPart]);
    }

    [Fact]
    public void Cobertura_SkipsMissingAndCorruptFilesInsteadOfFailing()
    {
        using var workspace = new TempWorkspace();
        var corrupt = workspace.WriteFile("cobertura-broken.xml", "<coverage><class filename=");

        var coverage = CoberturaReader.Read(
            [workspace.PathTo("absent.xml"), corrupt, Fixture.Path(Fixture.Cobertura)], TwoParts);

        Assert.Equal(300, coverage[MarkingMap.OverallPart].LinesTotal);
    }

    [Fact]
    public void Cobertura_CountsTheSamePathTwiceOnlyOnce()
    {
        // The judge globs TestResults for coverage.cobertura.xml and a careless glob returns the same file
        // more than once. Summing it twice doubled total and covered while leaving the rate intact, so the
        // platform stored and displayed line counts that contradicted the report's own lines-valid.
        var once = CoberturaReader.Read([Fixture.Path(Fixture.Cobertura)], TwoParts);
        var twice = CoberturaReader.Read(
            [Fixture.Path(Fixture.Cobertura), Fixture.Path(Fixture.Cobertura)], TwoParts);

        Assert.Equal(once[MarkingMap.OverallPart], twice[MarkingMap.OverallPart]);
        Assert.Equal(once["services"], twice["services"]);
        Assert.Equal(300, twice[MarkingMap.OverallPart].LinesTotal);
    }

    [Fact]
    public void Cobertura_CountsIdenticalReportsAtDifferentPathsOnlyOnce()
    {
        // The real shape of the bug: coverlet's report under a run GUID, and the byte-identical copy VSTest
        // attaches to the TRX under <run>/In/<host>/. Two paths, one measurement.
        using var workspace = new TempWorkspace();
        var content = File.ReadAllText(Fixture.Path(Fixture.Cobertura));
        var original = workspace.WriteFile("run-guid-coverage.cobertura.xml", content);
        var attachment = workspace.WriteFile("trx-attachment-coverage.cobertura.xml", content);

        var coverage = CoberturaReader.Read([original, attachment], TwoParts);

        Assert.Equal(new CoverageStats(269, 300), coverage[MarkingMap.OverallPart]);
    }

    [Fact]
    public void Cobertura_StillSumsReportsWithDifferentContent()
    {
        // Deduplication is by content, not "take the first": a multi-project run legitimately produces one
        // report per project and those must still add up.
        using var workspace = new TempWorkspace();
        var extra = workspace.WriteFile(
            "other.cobertura.xml",
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="Services/Extra.cs">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var coverage = CoberturaReader.Read([Fixture.Path(Fixture.Cobertura), extra], TwoParts);

        Assert.Equal(new CoverageStats(270, 302), coverage[MarkingMap.OverallPart]);
    }

    // ---------- TRX ----------

    [Fact]
    public void Trx_CountsTimeoutAsAFailureRatherThanDroppingIt()
    {
        var counts = TrxReader.Read([Fixture.Path(Fixture.Trx)], TwoParts);

        // The Timeout result belongs to Acme.Program.SmokeTests. Dropping it, as the original did, would
        // leave this part with no tests at all and a pass rate of 0 that means "nothing ran" rather than
        // "it failed" - and would let a suite hide failures by hanging.
        Assert.Equal(new TestCounts(0, 1, 0), counts["program"]);
    }

    [Fact]
    public void Trx_KeepsSkippedTestsOutOfThePassRateButStillReportsThem()
    {
        var counts = TrxReader.Read([Fixture.Path(Fixture.Trx)], TwoParts);
        var services = counts["services"];

        Assert.Equal(new TestCounts(1, 1, 1), services);
        Assert.Equal(2, services.Total);
        Assert.Equal(0.5, services.PassRate);
    }

    [Fact]
    public void Trx_CountsUnroutedTestsInOverallOnly()
    {
        var counts = TrxReader.Read([Fixture.Path(Fixture.Trx)], TwoParts);
        var overall = counts[MarkingMap.OverallPart];

        // Acme.Other.MiscTests routes nowhere but still ran.
        Assert.Equal(new TestCounts(2, 2, 1), overall);
        Assert.Equal(0.5, overall.PassRate);
    }

    [Fact]
    public void Trx_WithNoFiles_YieldsNoCounts()
    {
        Assert.Empty(TrxReader.Read([], TwoParts));
    }

    // ---------- Stryker ----------

    [Fact]
    public void Stryker_ClassifiesEveryStatusAndCountsTimeoutAsAKill()
    {
        var mutation = StrykerReader.Read(Fixture.Path(Fixture.Stryker), TwoParts);
        var services = mutation["services"];

        Assert.Equal(new MutationStats(Killed: 3, Survived: 1, Timeout: 1, NoCoverage: 2, Other: 1), services);

        // Covered excludes NoCoverage and CompileError; a timeout counts as detected.
        Assert.Equal(5, services.Covered);
        Assert.Equal(0.8, services.KillRate);
    }

    [Fact]
    public void Stryker_SumsPartsIntoOverall()
    {
        var mutation = StrykerReader.Read(Fixture.Path(Fixture.Stryker), TwoParts);
        var overall = mutation[MarkingMap.OverallPart];

        Assert.Equal(new MutationStats(4, 2, 1, 2, 1), overall);
        Assert.Equal(7, overall.Covered);
        Assert.Equal(5.0 / 7.0, overall.KillRate, 10);
    }

    [Fact]
    public void Stryker_IgnoresFilesWithoutAMutantsArray()
    {
        var mutation = StrykerReader.Read(Fixture.Path(Fixture.Stryker), TwoParts);

        // Unmutated.cs has no mutants; it must not contribute a zero-mutant entry.
        Assert.Equal(4 + 2 + 1 + 2 + 1, Total(mutation[MarkingMap.OverallPart]));
    }

    [Fact]
    public void Stryker_WithAMissingReport_YieldsEmptyStatsRatherThanFailing()
    {
        Assert.Empty(StrykerReader.Read("/nowhere/mutation-report.json", TwoParts));
        Assert.Empty(StrykerReader.Read(null, TwoParts));
    }

    [Fact]
    public void Stryker_WithACorruptReport_IsSkippedLikeTheOtherReaders()
    {
        using var workspace = new TempWorkspace();
        var corrupt = workspace.WriteFile("stryker-broken.json", "{\"files\": {");

        // The original parsed this one outside its try/catch, so a truncated report turned a scored
        // submission into a failed run - the harshest outcome for the least essential input.
        Assert.Empty(StrykerReader.Read(corrupt, TwoParts));
    }

    private static int Total(MutationStats stats) =>
        stats.Killed + stats.Survived + stats.Timeout + stats.NoCoverage + stats.Other;
}
