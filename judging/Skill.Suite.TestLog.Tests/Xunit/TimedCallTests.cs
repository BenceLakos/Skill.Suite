using Skill.Suite.TestLog.Tests.Support;
using Skill.Suite.TestLog.Xunit;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Xunit;

/// <summary>
/// Covers the per-call wall clock: a submission that never returns must cost the competitor the one test case
/// it hung in, not every test after it.
/// </summary>
/// <remarks>
/// Budgets here are fractions of a second so the suite stays fast. The tally of abandoned calls is
/// process-wide, so every test that abandons one resets it — otherwise a later test would find the guard
/// already stood down and would park forever.
/// </remarks>
public sealed class TimedCallTests : EventFixture
{
    [Fact]
    public void AHangingCall_FailsThatTestAndTheNextOneStillRuns()
    {
        using var budget = Budget("0.2");
        using var hanging = new HangingProbeService();

        try
        {
            using (var scope = new FixtureScope<MethodAnnotatedProbe>())
            {
                using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.GradedVisibleTest), scope))
                {
                    var service = hanging.WithCallLogging<IProbeService>();
                    Assert.Throws<TimeoutException>(() => service.Echo(7));
                }

                using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.UngradedTest), scope))
                {
                    var service = new WellBehavedProbeService().WithCallLogging<IProbeService>();
                    Assert.Equal(7, service.Echo(7));
                }
            }

            var finishes = LinesFor("finish-unit-test");

            // The whole point: the hang is one red row, and the run carried on to the next test case.
            Assert.Contains("\"outcome\":\"failed\"", finishes[0], StringComparison.Ordinal);
            Assert.Contains("\"outcome\":\"passed\"", finishes[1], StringComparison.Ordinal);
            Assert.Contains("\"testsRun\":2", LineFor("finish-fixture"), StringComparison.Ordinal);
        }
        finally
        {
            TimedCall.ResetAbandonedCalls();
        }
    }

    [Fact]
    public void AHangingCall_IsLoggedAsAThrownTimeoutNamingTheCall()
    {
        using var budget = Budget("0.2");
        using var hanging = new HangingProbeService();

        try
        {
            using (var scope = new FixtureScope<MethodAnnotatedProbe>())
            using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.GradedVisibleTest), scope))
            {
                var service = hanging.WithCallLogging<IProbeService>();
                Assert.Throws<TimeoutException>(() => service.Echo(7));
            }

            var call = LineFor("call");
            var finish = LineFor("finish-unit-test");

            // A non-empty `threw` is what promotes the unit to errored in both consumers, so a timeout needs
            // no new event kind and no parser change on either side.
            Assert.Contains("\"target\":\"IProbeService.Echo\"", call, StringComparison.Ordinal);
            Assert.Contains("\"arguments\":[7]", call, StringComparison.Ordinal);
            Assert.Contains("\"threw\":\"TimeoutException\"", call, StringComparison.Ordinal);

            Assert.Contains("\"outcome\":\"failed\"", finish, StringComparison.Ordinal);
            Assert.Contains(
                "IProbeService.Echo(7) did not return within 0.2s - most likely an endless loop",
                finish,
                StringComparison.Ordinal);
        }
        finally
        {
            TimedCall.ResetAbandonedCalls();
        }
    }

    [Fact]
    public void ACallThatReturnsInsideTheBudget_Passes()
    {
        using var budget = Budget("5");
        using var slow = new HangingProbeService { Delay = TimeSpan.FromMilliseconds(150) };

        using (var scope = new FixtureScope<MethodAnnotatedProbe>())
        using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.GradedVisibleTest), scope))
        {
            var service = slow.WithCallLogging<IProbeService>();
            Assert.Equal(3, service.Echo(3));
        }

        Assert.Contains("\"outcome\":\"passed\"", LineFor("finish-unit-test"), StringComparison.Ordinal);
        Assert.DoesNotContain("threw", LineFor("call"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheBudget_ComesFromTheEnvironment()
    {
        using var slow = new HangingProbeService { Delay = TimeSpan.FromMilliseconds(400) };

        try
        {
            using (Budget("0.1"))
            {
                Assert.Throws<TimeoutException>(() => slow.WithCallLogging<IProbeService>().Echo(1));
            }

            using (Budget("5"))
            {
                Assert.Equal(1, slow.WithCallLogging<IProbeService>().Echo(1));
            }
        }
        finally
        {
            TimedCall.ResetAbandonedCalls();
        }
    }

    [Fact]
    public void ABudgetOfZero_RunsTheCallOnTheCallingThreadWithNoGuardAtAll()
    {
        var service = new WellBehavedProbeService();

        using (Budget("0"))
        {
            service.WithCallLogging<IProbeService>().Work();
            Assert.Equal(Environment.CurrentManagedThreadId, service.LastCallThreadId);
        }

        // And the default, for contrast: the call is handed to a thread the harness can walk away from.
        using (Budget(null))
        {
            service.WithCallLogging<IProbeService>().Work();
            Assert.NotEqual(Environment.CurrentManagedThreadId, service.LastCallThreadId);
        }
    }

    [Fact]
    public void AnAbandonedCallThatFaultsLater_DoesNotSpoilTheTestRunningByThen()
    {
        // The abandoned thread keeps the ExecutionContext it was started with, so TestLog.Current on it is
        // frozen at the test that hung. Without that, FirstChanceException would stash the late throw on
        // whichever test happened to be running and fail an innocent one.
        using var budget = Budget("0.2");
        using var hanging = new HangingProbeService { ThrowWhenReleased = true };

        try
        {
            using var scope = new FixtureScope<MethodAnnotatedProbe>();

            using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.GradedVisibleTest), scope))
            {
                Assert.Throws<TimeoutException>(hanging.WithCallLogging<IProbeService>().Work);
            }

            using (var next = new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.UngradedTest), scope))
            {
                next.Logger.AssertEqual(1, 1);

                // Let the abandoned call fault while this test is the current one. Finished is set after the
                // throw, so waiting on it means the harness has already seen whatever it was going to see.
                hanging.Release();
                Assert.True(hanging.Finished.Wait(TimeSpan.FromSeconds(5)));
            }

            var finishes = LinesFor("finish-unit-test");
            Assert.Contains("\"outcome\":\"failed\"", finishes[0], StringComparison.Ordinal);
            Assert.Contains("\"outcome\":\"passed\"", finishes[1], StringComparison.Ordinal);
        }
        finally
        {
            TimedCall.ResetAbandonedCalls();
        }
    }

    [Fact]
    public void OnceTooManyCallsHaveBeenAbandoned_TheGuardStandsDownAndSaysSo()
    {
        using var budget = Budget("0.05");
        using var hanging = new HangingProbeService();
        using var slow = new HangingProbeService { Delay = TimeSpan.FromMilliseconds(400) };

        try
        {
            var service = hanging.WithCallLogging<IProbeService>();
            for (var i = 0; i < TimedCall.MaxAbandonedCalls; i++)
            {
                Assert.Throws<TimeoutException>(service.Work);
            }

            Assert.Equal(TimedCall.MaxAbandonedCalls, TimedCall.AbandonedCalls);
            Assert.Contains("\"event\":\"marker-error\"", LineFor("marker-error"), StringComparison.Ordinal);
            Assert.Contains("abandoned", LineFor("marker-error"), StringComparison.Ordinal);

            // A call far beyond the budget now completes instead of being abandoned: the guard has handed the
            // problem back to the judge's suite wall clock rather than leaking a ninth thread.
            Assert.Equal(2, slow.WithCallLogging<IProbeService>().Echo(2));
            Assert.Equal(TimedCall.MaxAbandonedCalls, TimedCall.AbandonedCalls);
        }
        finally
        {
            TimedCall.ResetAbandonedCalls();
        }
    }

    private static EnvironmentOverride Budget(string? seconds) =>
        new(TimedCall.TimeoutVariable, seconds);

    private static FakeTestOutputHelper Helper(string testName) =>
        new($"Skill.Suite.TestLog.Tests.Support.MethodAnnotatedProbe.{testName}");

    private static string[] LinesFor(string eventName) =>
        [.. NormalizedLines().Where(line => line.Contains($"\"event\":\"{eventName}\"", StringComparison.Ordinal))];

    private static string LineFor(string eventName) => LinesFor(eventName).Last();
}
