using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

// Inside a namespace under Skill.Suite.TestLog, the simple name `TestLog` binds to the *namespace*
// (found by walking the enclosing chain) and shadows the harness type. Consumers normally never name
// the type - they use the inherited `Log` property - but anything that does needs this alias.
using HarnessTestLog = Skill.Suite.TestLog.Xunit.TestLog;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Test classes shaped exactly like a graded suite, instantiated directly so the harness can be
/// observed without spawning a second test run.
/// </summary>
/// <remarks>
/// These deliberately mirror the required consumer shape from the package README:
/// <c>sealed class XTests : LoggedTest&lt;XTests&gt;, IClassFixture&lt;FixtureScope&lt;XTests&gt;&gt;</c>.
/// </remarks>
internal sealed class MethodAnnotatedProbe(ITestOutputHelper output, FixtureScope<MethodAnnotatedProbe> scope)
    : LoggedTest<MethodAnnotatedProbe>(output, scope)
{
    internal const string GradedVisibleTest = nameof(Graded_AndVisible_Case);
    internal const string GradedHiddenTest = nameof(Graded_ButHidden_Case);
    internal const string UngradedTest = nameof(Ungraded_Case);

    [Aspect("A1.1", CompetitorVisible = true)]
    internal void Graded_AndVisible_Case() { }

    [Aspect("A1.2")]
    internal void Graded_ButHidden_Case() { }

    internal void Ungraded_Case() { }

    /// <summary>The protected per-test log, surfaced so tests can drive the assertion wrappers directly.</summary>
    internal HarnessTestLog Logger => Log;

    internal void Fail() => Log.AssertEqual(1, 2);

    internal void CallInto(IProbeService service) => service.Work();
}

/// <summary>Every test in this fixture inherits the class-level aspect unless it declares its own.</summary>
[Aspect("B1.1", CompetitorVisible = true)]
internal sealed class ClassAnnotatedProbe(ITestOutputHelper output, FixtureScope<ClassAnnotatedProbe> scope)
    : LoggedTest<ClassAnnotatedProbe>(output, scope)
{
    internal const string InheritsClassAspectTest = nameof(Inherits_ClassAspect_Case);
    internal const string OverridesClassAspectTest = nameof(Overrides_ClassAspect_Case);

    internal void Inherits_ClassAspect_Case() { }

    [Aspect("C2.3")]
    internal void Overrides_ClassAspect_Case() { }
}

/// <summary>A service the proxy can wrap.</summary>
internal interface IProbeService
{
    void Work();

    string Describe();

    /// <summary>Takes an argument, so a timeout message has something to render.</summary>
    int Echo(int value);
}

internal sealed class ThrowingProbeService : IProbeService
{
    internal const string Message = "the service exploded";

    public void Work() => throw new InvalidOperationException(Message);

    public string Describe() => "throwing";

    public int Echo(int value) => throw new InvalidOperationException(Message);
}

internal sealed class NullReturningProbeService : IProbeService
{
    public void Work() { }

    // Declared non-nullable but returns null: the anti-gaming check must turn this into a throw.
    public string Describe() => null!;

    public int Echo(int value) => value;
}

internal sealed class WellBehavedProbeService : IProbeService
{
    /// <summary>Thread the last call ran on — the guard runs calls on one of its own, a disabled guard does not.</summary>
    internal int LastCallThreadId { get; private set; }

    public void Work() => LastCallThreadId = Environment.CurrentManagedThreadId;

    public string Describe()
    {
        LastCallThreadId = Environment.CurrentManagedThreadId;
        return "ok";
    }

    public int Echo(int value)
    {
        LastCallThreadId = Environment.CurrentManagedThreadId;
        return value;
    }
}

/// <summary>
/// Stands in for a submission that never returns, without actually looping forever: every call blocks on a
/// gate the test releases, so the abandoned threads this produces end their lives with the test that made them
/// instead of spinning for the rest of the run.
/// </summary>
internal sealed class HangingProbeService : IProbeService, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: false);

    /// <summary>Set once a blocked call has finished unblocking — after its throw, when one was asked for.</summary>
    internal ManualResetEventSlim Finished { get; } = new(initialState: false);

    /// <summary>Makes a released call throw, reproducing an abandoned thread that faults long after the fact.</summary>
    internal bool ThrowWhenReleased { get; init; }

    /// <summary>Delay before the call returns, for the "completes just inside the budget" case.</summary>
    internal TimeSpan Delay { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;

    /// <summary>Lets every parked call through.</summary>
    internal void Release() => _gate.Set();

    public void Work() => Block();

    public string Describe()
    {
        Block();
        return "hanging";
    }

    public int Echo(int value)
    {
        Block();
        return value;
    }

    /// <summary>Releases the parked calls so no thread outlives the test that abandoned it.</summary>
    /// <remarks>
    /// The two events are deliberately not disposed: a thread may still be inside <c>Wait</c>, and disposing
    /// the handle underneath it would swap the failure this probe is simulating for an unrelated one.
    /// </remarks>
    public void Dispose() => _gate.Set();

    private void Block()
    {
        _gate.Wait(Delay);
        try
        {
            if (ThrowWhenReleased) throw new InvalidOperationException("late throw from an abandoned call");
        }
        finally
        {
            // Runs after FirstChanceException has already fired for the throw above, so a test that waits on
            // this knows the harness has seen whatever the abandoned thread did.
            Finished.Set();
        }
    }
}
