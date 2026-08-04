using Skill.Suite.TestLog.Protocol;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// One scripted judgement run covering every event kind and every verdict path.
/// </summary>
/// <remarks>
/// This is the single definition of the golden stream. It is asserted against <c>Golden/protocol-v1.jsonl</c>,
/// and the same golden file is replayed through the platform's real parser by the conformance tests in
/// <c>Skill.Suite.Application.Tests</c> — so producer and consumer are pinned to one artifact instead of two
/// drifting copies.
/// </remarks>
internal static class ScriptedRun
{
    internal const string Fixture = "DemoTests";

    internal const string PassingTest = "Compute_TwoPlusTwo_ReturnsFour";
    internal const string FailingTest = "Compute_Overflow_Saturates";
    internal const string ErroredTest = "Compute_NullInput_Throws";
    internal const string UnannotatedTest = "Diagnostics_Smoke_DoesNotThrow";

    internal static void Emit()
    {
        TestLogger.StartFixture(Fixture);

        // Passing, graded, visible to the competitor.
        TestLogger.StartUnitTest(Fixture, PassingTest, "A1.1", aspectCompetitorVisible: true);
        TestLogger.Call(Fixture, PassingTest, "ICalculator.Compute", [2, 2], returned: 4, hasReturned: true);
        TestLogger.Assertion(Fixture, PassingTest, AssertionKinds.Equal, 4, 4, passed: true);
        TestLogger.FinishUnitTest(
            Fixture, PassingTest, TestLogOutcome.Passed, 12, null, "A1.1", aspectCompetitorVisible: true);

        // Failing by assertion, graded, not visible.
        TestLogger.StartUnitTest(Fixture, FailingTest, "A1.2");
        TestLogger.Assertion(Fixture, FailingTest, AssertionKinds.Equal, int.MaxValue, -1, passed: false);
        TestLogger.FinishUnitTest(
            Fixture, FailingTest, TestLogOutcome.Failed, 7, "Assert.Equal() Failure: Values differ", "A1.2");

        // Errored through the system under test throwing, graded, visible.
        TestLogger.StartUnitTest(Fixture, ErroredTest, "A1.3", aspectCompetitorVisible: true);
        TestLogger.Call(
            Fixture, ErroredTest, "ICalculator.Compute", [null, 2],
            threw: "ArgumentNullException: Value cannot be null. (Parameter 'left')");
        TestLogger.FinishUnitTest(
            Fixture, ErroredTest, TestLogOutcome.Errored, 3,
            "ArgumentNullException: Value cannot be null. (Parameter 'left')",
            "A1.3", aspectCompetitorVisible: true);

        // Ungraded: carries no aspect at all, so its line has no aspect fields.
        TestLogger.StartUnitTest(Fixture, UnannotatedTest);
        TestLogger.Assertion(
            Fixture, UnannotatedTest, AssertionKinds.NotNull, AssertionSentinels.NonNull, "ok", passed: true);
        TestLogger.FinishUnitTest(Fixture, UnannotatedTest, TestLogOutcome.Passed, 1, null);

        TestLogger.FinishFixture(Fixture, testsRun: 4, testsPassed: 2, testsFailed: 2, durationMs: 23);

        // The rollup: no part, so the consumer defaults it to "overall".
        TestLogger.Emit(new TestSummaryEvent(
            Part: null, Value: 0.5, Total: 4, Passed: 2, Failed: 2, Skipped: 0));

        TestLogger.Emit(new ScoreEvent(MetricEvent.OverallPart, 0.5));
    }
}
