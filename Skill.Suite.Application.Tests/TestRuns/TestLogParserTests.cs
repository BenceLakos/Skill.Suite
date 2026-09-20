using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Skill.Suite.Domain.TestRuns;
using Xunit;

namespace Skill.Suite.Application.Tests.TestRuns;

/// <summary>
/// Covers the one place the event protocol is interpreted. Every field name here is read case-sensitively
/// with no naming policy, so these are compatibility assertions against the judging layer.
/// </summary>
public sealed class TestLogParserTests
{
    [Fact]
    public void MarkerError_IsRecordedAsADiagnostic()
    {
        var run = NewRun();

        Apply(run, """{"timestamp":"2026-07-30T10:00:00.000Z","event":"marker-error","detail":"restore failed: NU1101"}""");

        var fixture = Assert.Single(run.Fixtures);
        Assert.Equal(TestFixtureKind.Diagnostics, fixture.Kind);
        Assert.Equal(TestRun.DiagnosticsFixtureName, fixture.Name);

        var unit = Assert.Single(fixture.UnitTests);
        var evt = Assert.Single(unit.Events);
        Assert.Equal(TestEventKind.Error, evt.Kind);
        Assert.Equal("restore failed: NU1101", evt.Detail);
    }

    [Fact]
    public void MarkerError_WithoutDetail_FallsBackToTheRawLine()
    {
        var run = NewRun();

        Apply(run, """{"event":"marker-error"}""");

        var evt = run.Fixtures.Single().UnitTests.Single().Events.Single();
        Assert.Contains("marker-error", evt.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkerError_TwiceGroupsIntoOneFixture()
    {
        var run = NewRun();

        Apply(run, """{"event":"marker-error","detail":"first"}""");
        Apply(run, """{"event":"marker-error","detail":"second"}""");

        var fixture = Assert.Single(run.Fixtures);
        Assert.Equal(2, fixture.UnitTests.Single().Events.Count);
    }

    [Fact]
    public void Aspect_IsPersistedFromStartAndFinish()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"F","test":"T","aspect":"C2.1","aspectVisible":true}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"passed","durationMs":5,"error":null,"aspect":"C2.1","aspectVisible":true}""");

        var unit = run.Fixtures.Single().UnitTests.Single();
        Assert.Equal("C2.1", unit.Aspect);
        Assert.True(unit.AspectCompetitorVisible);
        Assert.Equal(TestOutcome.Passed, unit.Outcome);
    }

    [Fact]
    public void Aspect_ArrivingOnlyOnFinish_IsStillRecorded()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"F","test":"T"}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"passed","aspect":"A1.9"}""");

        Assert.Equal("A1.9", run.Fixtures.Single().UnitTests.Single().Aspect);
    }

    [Fact]
    public void UnannotatedTest_HasNoAspect()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"F","test":"T"}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"passed"}""");

        var unit = run.Fixtures.Single().UnitTests.Single();
        Assert.Null(unit.Aspect);
        Assert.False(unit.AspectCompetitorVisible);
    }

    [Fact]
    public void MetricPart_DoesNotHijackATestFixtureOfTheSameName()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"overall","test":"T","aspect":"A1.1","aspectVisible":true}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"overall","test":"T","outcome":"passed"}""");
        Apply(run, """{"event":"score","part":"overall","value":0.9}""");

        // Two rows with the same name but different kinds. Before kind-scoping, the score event attached to
        // the test fixture and set its quality, silently overriding the competitor's real result.
        Assert.Equal(2, run.Fixtures.Count);

        var tests = run.Fixtures.Single(f => f.Kind == TestFixtureKind.Tests);
        var metrics = run.Fixtures.Single(f => f.Kind == TestFixtureKind.Metrics);

        Assert.Null(tests.Quality);
        Assert.Equal(0.9, metrics.Quality);
    }

    [Fact]
    public void FixtureScopedMetrics_LandOnTheTestClassItself()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"CalculatorTests","test":"Adds"}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"CalculatorTests","test":"Adds","outcome":"passed"}""");
        Apply(run, """{"event":"coverage","value":0.75,"total":8,"covered":6,"fixture":"CalculatorTests"}""");
        Apply(run, """{"event":"mutation","value":0.6667,"total":3,"covered":3,"killed":2,"fixture":"CalculatorTests"}""");

        var fixture = Assert.Single(run.Fixtures);
        Assert.Equal(TestFixtureKind.Tests, fixture.Kind);
        Assert.Equal(0.75, fixture.LineCoverage);
        Assert.Equal(0.6667, fixture.MutationScore);

        // A measurement, never a verdict: quality is still only ever set by a part's score event.
        Assert.Null(fixture.Quality);
    }

    [Fact]
    public void FixtureScopedMetrics_DoNotCreateAMetricPart()
    {
        var run = NewRun();

        Apply(run, """{"event":"coverage","part":"services","value":0.9,"total":10,"covered":9}""");
        Apply(run, """{"event":"coverage","value":0.5,"total":10,"covered":5,"fixture":"services"}""");

        // Same name, two meanings. Routing the fixture-scoped one by `part` would have merged a competitor's
        // test class into the scoring part of the same name - exactly what TestFixtureKind exists to prevent.
        Assert.Equal(2, run.Fixtures.Count);
        Assert.Null(run.Fixtures.Single(f => f.Kind == TestFixtureKind.Metrics).LineCoverage);
        Assert.Equal(0.5, run.Fixtures.Single(f => f.Kind == TestFixtureKind.Tests).LineCoverage);
    }

    [Fact]
    public void FixtureScopedMetrics_CreateTheFixtureWhenTheStreamHadNone()
    {
        var run = NewRun();

        // The marker appends after the test host has exited, so a class the harness never opened - one whose
        // tests were all filtered out, say - can still have been measured.
        Apply(run, """{"event":"mutation","value":1,"total":2,"covered":2,"killed":2,"fixture":"SmokeTests"}""");

        var fixture = Assert.Single(run.Fixtures);
        Assert.Equal(TestFixtureKind.Tests, fixture.Kind);
        Assert.Equal("SmokeTests", fixture.Name);
        Assert.Equal(1.0, fixture.MutationScore);
    }

    [Fact]
    public void FixtureScopedMetrics_KeepTheirRawPayloadOnTheMetricsUnit()
    {
        var run = NewRun();

        Apply(run, """{"event":"coverage","value":0.75,"total":8,"covered":6,"fixture":"CalculatorTests"}""");
        Apply(run, """{"event":"mutation","value":0.5,"total":2,"covered":2,"killed":1,"fixture":"CalculatorTests"}""");

        var unit = Assert.Single(run.Fixtures.Single().UnitTests);
        Assert.Equal(TestRun.QualityUnitName, unit.Name);
        Assert.Equal(2, unit.Events.Count);
        Assert.All(unit.Events, e => Assert.Equal(TestEventKind.Metric, e.Kind));
        Assert.Contains(unit.Events, e => e.Target == "coverage" && e.Payload!.Contains("\"fixture\":\"CalculatorTests\""));
    }

    [Fact]
    public void PartScopedMetrics_AreUnchangedByFixtureScoping()
    {
        var run = NewRun();

        Apply(run, """{"event":"coverage","part":"overall","value":0.9,"total":10,"covered":9}""");
        Apply(run, """{"event":"score","part":"overall","value":0.8}""");

        var fixture = Assert.Single(run.Fixtures);
        Assert.Equal(TestFixtureKind.Metrics, fixture.Kind);
        Assert.Equal(0.8, fixture.Quality);

        // The part rollup's coverage stays a stored payload, not a column: only a fixture-scoped event fills
        // LineCoverage, so the two can never be confused in the UI.
        Assert.Null(fixture.LineCoverage);
    }

    [Fact]
    public void OverLongNames_AreTruncatedRatherThanLosingTheRun()
    {
        var run = NewRun();
        var longName = new string('x', TestRunLimits.NameMaxLength + 200);

        Apply(run, $$"""{"event":"start-unit-test","fixture":"{{longName}}","test":"{{longName}}"}""");

        var fixture = Assert.Single(run.Fixtures);
        // A name wider than the column would throw on SaveChanges and take every fixture in the run with it.
        Assert.Equal(TestRunLimits.NameMaxLength, fixture.Name.Length);
        Assert.Equal(TestRunLimits.NameMaxLength, fixture.UnitTests.Single().Name.Length);
    }

    [Fact]
    public void OverLongAspect_IsTruncated()
    {
        var run = NewRun();
        var longAspect = new string('a', TestRunLimits.AspectMaxLength + 50);

        Apply(run, $$"""{"event":"start-unit-test","fixture":"F","test":"T","aspect":"{{longAspect}}"}""");

        Assert.Equal(TestRunLimits.AspectMaxLength, run.Fixtures.Single().UnitTests.Single().Aspect!.Length);
    }

    [Fact]
    public void OverLongDiagnostic_IsTruncated()
    {
        var run = NewRun();
        var detail = new string('d', TestRunLimits.DetailMaxLength + 500);

        Apply(run, $$"""{"event":"marker-error","detail":"{{detail}}"}""");

        var evt = run.Fixtures.Single().UnitTests.Single().Events.Single();
        Assert.Equal(TestRunLimits.DetailMaxLength, evt.Detail!.Length);
    }

    [Fact]
    public void ThrownCall_ErrorsTheUnitAndAnOptimisticOutcomeCannotUndoIt()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"F","test":"T"}""");
        Apply(run, """{"event":"call","fixture":"F","test":"T","target":"I.M","arguments":[],"threw":"InvalidOperationException"}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"passed"}""");

        Assert.Equal(TestOutcome.Errored, run.Fixtures.Single().UnitTests.Single().Outcome);
    }

    [Fact]
    public void FailedAssertion_FailsTheUnit()
    {
        var run = NewRun();

        Apply(run, """{"event":"start-unit-test","fixture":"F","test":"T"}""");
        Apply(run, """{"event":"assertion","fixture":"F","test":"T","kind":"equal","expected":1,"actual":2,"passed":false}""");
        Apply(run, """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"passed"}""");

        Assert.Equal(TestOutcome.Failed, run.Fixtures.Single().UnitTests.Single().Outcome);
    }

    [Fact]
    public void UnknownEventKinds_AreStillIgnored()
    {
        var run = NewRun();

        Apply(run, """{"event":"log-error"}""");
        Apply(run, """{"event":"marker-warning","kind":"no-trx"}""");
        Apply(run, """{"event":"totally-made-up"}""");

        // The protocol promises unknown kinds are ignored. A catch-all would turn a typo into a fixture.
        Assert.Empty(run.Fixtures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"no-event":true}""")]
    public void MalformedLines_AreDropped(string line)
    {
        var run = NewRun();

        Apply(run, line);

        Assert.Empty(run.Fixtures);
    }

    private static void Apply(TestRun run, string line) => TestLogParser.Apply(run, line);

    private static TestRun NewRun() =>
        TestRun.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.test/r.git", "r", "main", "abc123", "f", "img:1");
}
