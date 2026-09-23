using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// Runs one call into the system under test under a wall clock, so a submission that never returns costs the
/// competitor that single test case instead of every test after it.
/// </summary>
/// <remarks>
/// <para>
/// The failure this exists for is real and was scored: a submission whose <c>At</c> returned 0 for every index
/// made a linear search for the value 1 loop forever, the test host sat in that one test until the judge's
/// suite-level wall clock tore the container down, and the competitor lost every remaining test in the fixture
/// rather than the one that hung.
/// </para>
/// <para>
/// .NET has no <c>Thread.Abort</c>, so a synchronous endless loop cannot be stopped — only abandoned. The call
/// runs on a background thread, the caller waits for the budget, and on expiry the thread is left running and
/// the call fails with a <see cref="TimeoutException"/>. The abandoned thread keeps burning CPU (bounded by the
/// container's <c>--cpus</c> and <c>--pids-limit</c>), so after
/// <see cref="MaxAbandonedCalls"/> of them the guard stands down and the suite wall clock in <c>judge-lib.sh</c>
/// becomes the backstop again — with a <c>marker-error</c> saying so.
/// </para>
/// <para>
/// <b>An abandoned thread cannot pollute a later test.</b> <c>Thread.Start</c> captures the caller's
/// <see cref="ExecutionContext"/>, and <c>TestLog.Current</c> is an <see cref="AsyncLocal{T}"/>, so the worker
/// sees a frozen snapshot: the log of the test that started it. Nothing the test host does afterwards — the
/// <c>SetCurrent(null)</c> in <c>Dispose</c>, the next test's <c>SetCurrent</c> — reaches that thread. A stray
/// <c>call</c> event, or an exception seen by the <c>FirstChanceException</c> handler on an abandoned thread,
/// is therefore attributed to the already-failed test it came from and never to whichever test is running now.
/// </para>
/// <para>
/// Not covered: a hang in the test body itself, in a service constructor resolved by
/// <see cref="ServiceResolver"/> (that runs in a field initializer, before the test even starts), or after the
/// first <c>await</c> of a <see cref="System.Threading.Tasks.Task"/>-returning member — reflection hands such a
/// member's Task straight back, so what is timed is the synchronous part of the call, which is exactly the part
/// the proxy has ever observed. The suite wall clock still covers all three.
/// </para>
/// </remarks>
internal static class TimedCall
{
    /// <summary>Per-call budget, in seconds. Fractional values are accepted; zero or less disables the guard.</summary>
    internal const string TimeoutVariable = "JUDGE_CALL_TIMEOUT_SECONDS";

    /// <summary>
    /// Calls that were abandoned before the guard stands down and leaves hangs to the suite wall clock.
    /// </summary>
    /// <remarks>
    /// Each abandoned thread spins forever on two shared CPUs, so the cap is what stops a submission that
    /// loops in every method from starving the tests that could still have been marked. Eight is enough for a
    /// fixture where a handful of members hang and low enough that the survivors still get CPU.
    /// </remarks>
    internal const int MaxAbandonedCalls = 8;

    // Three orders of magnitude above what a graded call takes on a warmed container (the real runs report
    // durationMs 0 for whole test cases), so it cannot fire on a merely slow-but-correct implementation, and
    // small enough that several hangs still fit inside the 300s suite budget alongside the tests that pass.
    private const int DefaultTimeoutSeconds = 10;

    // A configured value beyond this is treated as "effectively never" rather than overflowing TimeSpan.
    private const double MaxTimeoutSeconds = 86_400;

    private const int MaxRenderedArgumentLength = 40;

    private static int _abandonedCalls;

    // Set on the worker thread only. A proxied call the submission makes from inside another proxied call runs
    // inline: the outer budget already covers it, and nesting a thread per level is how a chain of services
    // turns one hang into a thread per link.
    [ThreadStatic]
    private static bool _guarding;

    /// <summary>How many calls have been abandoned in this process.</summary>
    internal static int AbandonedCalls => Volatile.Read(ref _abandonedCalls);

    /// <summary>Clears the abandoned-call tally. For test isolation only.</summary>
    internal static void ResetAbandonedCalls() => Interlocked.Exchange(ref _abandonedCalls, 0);

    /// <summary>Invokes <paramref name="call"/> under the configured budget.</summary>
    /// <param name="target">Called member, for the timeout message — conventionally <c>IInterface.Method</c>.</param>
    /// <param name="arguments">Arguments as logged, rendered into the timeout message.</param>
    /// <param name="call">The invocation itself.</param>
    /// <param name="thrown">
    /// What the call threw, unwrapped from reflection's <see cref="TargetInvocationException"/>, or the
    /// <see cref="TimeoutException"/> synthesized when the budget expired. <see langword="null"/> on success.
    /// </param>
    /// <returns>Whatever the call returned, or <see langword="null"/> when it threw or was abandoned.</returns>
    internal static object? Invoke(
        string target, IReadOnlyList<object?> arguments, Func<object?> call, out Exception? thrown)
    {
        var budget = Budget();

        // Debugger.IsAttached: stepping through a service under a debugger takes minutes, and failing the test
        // the author is inspecting would make the harness unusable for the person writing the suite.
        if (budget <= TimeSpan.Zero || _guarding || Debugger.IsAttached || AbandonedCalls >= MaxAbandonedCalls)
        {
            return Run(call, out thrown);
        }

        object? result = null;
        Exception? failure = null;

        var worker = new Thread(() =>
        {
            _guarding = true;
            result = Run(call, out failure);
        })
        {
            // The point of the exercise: an abandoned thread must not keep the test host alive at shutdown.
            IsBackground = true,
            Name = $"judge-call {target}",
        };

        worker.Start();

        // Join rather than a wait handle: it is the timed wait and the memory barrier in one call, so the
        // locals the worker assigned are visible here once it returns true.
        if (worker.Join(budget))
        {
            thrown = failure;
            return result;
        }

        Abandon();
        thrown = Expired(target, arguments, budget);
        return null;
    }

    /// <remarks>
    /// The catch-all is load bearing twice over: it unwraps the <see cref="TargetInvocationException"/>
    /// reflection wraps a service throw in, and — on the worker thread — it is what stops a submission that
    /// throws *after* being abandoned from taking the whole test host down with an unhandled exception on a
    /// background thread.
    /// </remarks>
    private static object? Run(Func<object?> call, out Exception? thrown)
    {
        try
        {
            thrown = null;
            return call();
        }
        catch (TargetInvocationException invocation)
        {
            thrown = invocation.InnerException ?? invocation;
            return null;
        }
        catch (Exception exception)
        {
            thrown = exception;
            return null;
        }
    }

    private static void Abandon()
    {
        // Only on the transition, so the diagnostic appears once however many hangs follow it.
        if (Interlocked.Increment(ref _abandonedCalls) != MaxAbandonedCalls) return;

        TestLogger.MarkerError(
            $"{MaxAbandonedCalls} calls into the submission were abandoned after exceeding {TimeoutVariable} " +
            "and are still running inside the test host. Later calls are no longer guarded, so a further hang " +
            "ends the run on the judge's suite wall clock and the results after this point may be incomplete.");
    }

    private static TimeSpan Budget()
    {
        var raw = Environment.GetEnvironmentVariable(TimeoutVariable);
        if (string.IsNullOrWhiteSpace(raw)) return TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        // A value nobody can parse is a misconfigured image, not a request to run unguarded: fall back to the
        // default rather than silently restoring the behaviour this exists to fix.
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || double.IsNaN(seconds))
        {
            return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        }

        return seconds <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Math.Min(seconds, MaxTimeoutSeconds));
    }

    private static TimeoutException Expired(string target, IReadOnlyList<object?> arguments, TimeSpan budget) =>
        new($"{target}({string.Join(", ", arguments.Select(Render))}) did not return within " +
            $"{budget.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s - most likely an endless " +
            "loop. The call was abandoned and this test failed; the remaining tests still ran.");

    // Enough to identify which input hung, not a serializer: the `call` event alongside carries the real
    // arguments. Anything without a culture-invariant text form contributes its type name.
    private static string Render(object? argument) => argument switch
    {
        null => "null",
        string text => $"\"{Truncate(text)}\"",
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => argument.GetType().Name,
    };

    private static string Truncate(string text) =>
        text.Length <= MaxRenderedArgumentLength
            ? text
            : string.Concat(text.AsSpan(0, MaxRenderedArgumentLength), "...");
}
