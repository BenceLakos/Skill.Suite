using System.Diagnostics;
using Skill.Suite.TestLog.Protocol;
using Skill.Suite.TestLog;

// A judgement run with no test framework at all: reference one package, emit events, and the results
// show up in the Skill.Suite UI. Run it with LOG_DIRECTORY set to write events.jsonl, or without it
// to see the identical lines on stdout.
//
//     LOG_DIRECTORY=/tmp/quickstart dotnet run && cat /tmp/quickstart/events.jsonl

const string fixture = "QuickstartChecks";
const string addTest = "Add_TwoAndThree_ReturnsFive";
const string divideTest = "Divide_ByZero_ReturnsZero";

var fixtureClock = Stopwatch.StartNew();
TestLogger.StartFixture(fixture);

// A check that passes.
var addClock = Stopwatch.StartNew();
TestLogger.StartUnitTest(fixture, addTest, aspect: "A1.1", aspectCompetitorVisible: true);
var sum = Add(2, 3);
TestLogger.Call(fixture, addTest, "Calculator.Add", [2, 3], returned: sum, hasReturned: true);
var sumOk = sum == 5;
TestLogger.Assertion(fixture, addTest, AssertionKinds.Equal, 5, sum, passed: sumOk);
TestLogger.FinishUnitTest(
    fixture, addTest, sumOk ? TestLogOutcome.Passed : TestLogOutcome.Failed, addClock.ElapsedMilliseconds,
    sumOk ? null : $"expected 5, got {sum}", aspect: "A1.1", aspectCompetitorVisible: true);

// A check that fails, so the failure path is visible in the UI too.
var divideClock = Stopwatch.StartNew();
TestLogger.StartUnitTest(fixture, divideTest, aspect: "A1.2", aspectCompetitorVisible: true);
var quotient = Divide(1, 0);
TestLogger.Call(fixture, divideTest, "Calculator.Divide", [1, 0], returned: quotient, hasReturned: true);
var quotientOk = quotient == 0;
TestLogger.Assertion(fixture, divideTest, AssertionKinds.Equal, 0, quotient, passed: quotientOk);
TestLogger.FinishUnitTest(
    fixture, divideTest, quotientOk ? TestLogOutcome.Passed : TestLogOutcome.Failed, divideClock.ElapsedMilliseconds,
    quotientOk ? null : $"expected 0, got {quotient}", aspect: "A1.2", aspectCompetitorVisible: true);

var passed = (sumOk ? 1 : 0) + (quotientOk ? 1 : 0);
TestLogger.FinishFixture(fixture, testsRun: 2, testsPassed: passed, testsFailed: 2 - passed, fixtureClock.ElapsedMilliseconds);

// The score's value must be a number in 0..1 - that is what drives the competitor-visible indicator.
// A test-summary alongside it reports the tallies; the score reports only its verdict.
TestLogger.Emit(new TestSummaryEvent(
    Part: null, Value: passed / 2.0, Total: 2, Passed: passed, Failed: 2 - passed, Skipped: 0));

TestLogger.Emit(new ScoreEvent(MetricEvent.OverallPart, passed / 2.0));

return 0;

static int Add(int left, int right) => left + right;

// Deliberately wrong: returns 1 instead of the documented 0 sentinel, so the second check fails.
static int Divide(int left, int right) => right == 0 ? 1 : left / right;
