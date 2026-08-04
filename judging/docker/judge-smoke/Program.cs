using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Protocol;

// judge-smoke: emits a fixed, deterministic event stream and exits 0.
//
// It ignores the competitor checkout entirely. That is the whole point: it proves the platform wiring -
// webhook, clone, volume mounts, container run, parse, score bucket, live UI refresh - with zero
// dependency on any session's code. Wire a session to this image first; only once a push shows the results
// below in the UI is it worth debugging a real judge image.
//
// The stream deliberately covers all three verdict paths, because each reaches the UI differently:
//   passed  - the ordinary case
//   failed  - through an assertion with passed:false
//   errored - through a call carrying `threw`
// It also carries visible [Aspect] ids and a score event, so both routes to a competitor-visible bucket
// (aspect rollup, and an explicit quality) are exercised.

const string checksFixture = "SmokeChecks";
const string errorsFixture = "SmokeErrors";

const string passingTest = "Smoke_PassingCase_Passes";
const string failingTest = "Smoke_FailingCase_Fails";
const string erroredTest = "Smoke_ErroredCase_Throws";

TestLogger.StartFixture(checksFixture);

TestLogger.StartUnitTest(checksFixture, passingTest, "S1.1", aspectCompetitorVisible: true);
TestLogger.Call(checksFixture, passingTest, "ISmokeService.Add", [2, 2], returned: 4, hasReturned: true);
TestLogger.Assertion(checksFixture, passingTest, AssertionKinds.Equal, 4, 4, passed: true);
TestLogger.FinishUnitTest(
    checksFixture, passingTest, TestLogOutcome.Passed, 5, null, "S1.1", aspectCompetitorVisible: true);

TestLogger.StartUnitTest(checksFixture, failingTest, "S1.2", aspectCompetitorVisible: true);
TestLogger.Assertion(checksFixture, failingTest, AssertionKinds.Equal, 42, 41, passed: false);
TestLogger.FinishUnitTest(
    checksFixture, failingTest, TestLogOutcome.Failed, 4, "Assert.Equal() Failure: expected 42, got 41",
    "S1.2", aspectCompetitorVisible: true);

TestLogger.FinishFixture(checksFixture, testsRun: 2, testsPassed: 1, testsFailed: 1, durationMs: 9);

TestLogger.StartFixture(errorsFixture);

TestLogger.StartUnitTest(errorsFixture, erroredTest, "S1.3", aspectCompetitorVisible: true);
TestLogger.Call(
    errorsFixture, erroredTest, "ISmokeService.Divide", [1, 0],
    threw: "DivideByZeroException: Attempted to divide by zero.");
TestLogger.FinishUnitTest(
    errorsFixture, erroredTest, TestLogOutcome.Errored, 3, "DivideByZeroException: Attempted to divide by zero.",
    "S1.3", aspectCompetitorVisible: true);

TestLogger.FinishFixture(errorsFixture, testsRun: 1, testsPassed: 0, testsFailed: 1, durationMs: 4);

TestLogger.Emit(new TestSummaryEvent(
    Part: null, Value: 0.3333, Total: 3, Passed: 1, Failed: 2, Skipped: 0));

// The value is fixed at 0.5 so the expected bucket never moves: BucketFor(50) is High.
TestLogger.Emit(new ScoreEvent(MetricEvent.OverallPart, 0.5));

// Always 0. A smoke image that failed would be indistinguishable from broken wiring, which is the one
// thing it exists to rule out.
return 0;
