using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Skill.Suite.Application.TestRuns.MyTestRuns;
using Skill.Suite.Domain.TestRuns;
using Xunit;

namespace Skill.Suite.Application.Tests.TestRuns;

/// <summary>
/// The competitor-visible score. Its unit is the aspect, matching what the CIS report grades, so the bar a
/// competitor sees and the marking row cannot disagree.
/// </summary>
public sealed class ScoreBucketCalculatorTests
{
    [Fact]
    public void ExplicitQuality_Wins()
    {
        var run = NewRun();
        TestLogParser.Apply(run, """{"event":"score","part":"aqi","value":0.83}""");

        Assert.Equal(ScoreBucket.VeryHigh, Bucket(run, "aqi"));
    }

    // The quality is given as the literal JSON text on purpose. Interpolating a double formats it with the
    // current culture, and on a hu-HU machine that yields "0,5" - malformed JSON, so the event is dropped
    // and the test fails for a reason that has nothing to do with bucketing.
    [Theory]
    [InlineData("0.10", ScoreBucket.VeryLow)]
    [InlineData("0.30", ScoreBucket.Low)]
    [InlineData("0.50", ScoreBucket.High)]
    [InlineData("0.80", ScoreBucket.VeryHigh)]
    public void QualityMapsOntoTheBuckets(string quality, ScoreBucket expected)
    {
        var run = NewRun();
        TestLogParser.Apply(run, $$"""{"event":"score","part":"overall","value":{{quality}}}""");

        Assert.Equal(expected, Bucket(run, "overall"));
    }

    [Fact]
    public void VisibleAspects_AreRolledUp()
    {
        var run = NewRun();
        // Two visible aspects, one satisfied: 50% -> High.
        AddTest(run, "F", "T1", "A1.1", visible: true, "passed");
        AddTest(run, "F", "T2", "A1.2", visible: true, "failed");

        Assert.Equal(ScoreBucket.High, Bucket(run, "F"));
    }

    [Fact]
    public void OneFailingTest_SinksItsWholeAspect()
    {
        var run = NewRun();
        // A1.1 has two tests, one of which fails, so the aspect is not satisfied - the same rule the CIS
        // report applies. Counting tests instead of aspects would have reported 2 of 3.
        AddTest(run, "F", "T1", "A1.1", visible: true, "passed");
        AddTest(run, "F", "T2", "A1.1", visible: true, "failed");
        AddTest(run, "F", "T3", "A1.2", visible: true, "passed");

        Assert.Equal(ScoreBucket.High, Bucket(run, "F"));
    }

    [Fact]
    public void HiddenAspects_DoNotContribute()
    {
        var run = NewRun();
        // Only A1.1 is visible and it passes, so the competitor sees full marks even though a hidden aspect
        // failed. That is the point of the flag: grading is unaffected, feedback is limited.
        AddTest(run, "F", "T1", "A1.1", visible: true, "passed");
        AddTest(run, "F", "T2", "A1.2", visible: false, "failed");

        Assert.Equal(ScoreBucket.VeryHigh, Bucket(run, "F"));
    }

    [Fact]
    public void ErroredTest_DoesNotSatisfyItsAspect()
    {
        var run = NewRun();
        AddTest(run, "F", "T1", "A1.1", visible: true, "errored");

        Assert.Equal(ScoreBucket.VeryLow, Bucket(run, "F"));
    }

    [Fact]
    public void NoVisibleAspects_ReportsNone()
    {
        var run = NewRun();
        AddTest(run, "F", "T1", "A1.1", visible: false, "passed");
        AddTest(run, "F", "T2", null, visible: false, "passed");

        Assert.Equal(ScoreBucket.None, Bucket(run, "F"));
    }

    [Fact]
    public void UnannotatedFixture_ReportsNone()
    {
        var run = NewRun();
        AddTest(run, "F", "T1", null, visible: false, "passed");

        // Deliberate: a suite that declares no aspects has not said what it wants shown.
        Assert.Equal(ScoreBucket.None, Bucket(run, "F"));
    }

    [Fact]
    public void MetricFixtureWithoutQuality_ReportsNone()
    {
        var run = NewRun();
        TestLogParser.Apply(run, """{"event":"coverage","part":"aqi","total":100,"covered":50}""");

        Assert.Equal(ScoreBucket.None, Bucket(run, "aqi"));
    }

    [Fact]
    public void DiagnosticsFixture_ReportsNone()
    {
        var run = NewRun();
        TestLogParser.Apply(run, """{"event":"marker-error","detail":"boom"}""");

        Assert.Equal(ScoreBucket.None, Bucket(run, TestRun.DiagnosticsFixtureName));
    }

    [Fact]
    public void TheSyntheticQualityUnit_IsNotMistakenForATest()
    {
        var run = NewRun();
        TestLogParser.Apply(run, """{"event":"score","part":"overall","value":0.9}""");

        var fixture = run.Fixtures.Single();
        Assert.Equal(TestRun.QualityUnitName, fixture.UnitTests.Single().Name);
        Assert.Equal(ScoreBucket.VeryHigh, ScoreBucketCalculator.ForFixture(fixture));
    }

    private static void AddTest(TestRun run, string fixture, string test, string? aspect, bool visible, string outcome)
    {
        var aspectJson = aspect is null ? "" : $""","aspect":"{aspect}","aspectVisible":{visible.ToString().ToLowerInvariant()}""";
        TestLogParser.Apply(run, $$"""{"event":"start-unit-test","fixture":"{{fixture}}","test":"{{test}}"{{aspectJson}}}""");

        if (outcome == "errored")
        {
            TestLogParser.Apply(run,
                $$"""{"event":"call","fixture":"{{fixture}}","test":"{{test}}","target":"I.M","arguments":[],"threw":"Boom"}""");
        }

        TestLogParser.Apply(run,
            $$"""{"event":"finish-unit-test","fixture":"{{fixture}}","test":"{{test}}","outcome":"{{outcome}}"{{aspectJson}}}""");
    }

    private static ScoreBucket Bucket(TestRun run, string fixtureName) =>
        ScoreBucketCalculator.ForFixture(run.Fixtures.Last(f => f.Name == fixtureName));

    private static TestRun NewRun() =>
        TestRun.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.test/r.git", "r", "main", "abc", "f", "img:1");
}
