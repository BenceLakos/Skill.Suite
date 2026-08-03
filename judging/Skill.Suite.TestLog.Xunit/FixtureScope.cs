using System.Diagnostics;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// xUnit <c>IClassFixture</c> companion that brackets a test class's lifetime
/// with <c>start-fixture</c> / <c>finish-fixture</c> events.
/// </summary>
/// <typeparam name="TFixture">The test class this scope brackets — used to name the fixture.</typeparam>
public class FixtureScope<TFixture> : IDisposable
{
    private readonly string _name;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _testsRun;
    private int _testsPassed;
    private int _testsFailed;

    /// <summary>Opens the fixture and emits <c>start-fixture</c>. Invoked by xUnit's class-fixture machinery.</summary>
    public FixtureScope()
    {
        _name = typeof(TFixture).Name;
        TestLogger.StartFixture(_name);
    }

    internal void RecordTest(bool passed)
    {
        Interlocked.Increment(ref _testsRun);
        if (passed) Interlocked.Increment(ref _testsPassed);
        else Interlocked.Increment(ref _testsFailed);
    }

    /// <summary>Closes the fixture and emits <c>finish-fixture</c> with its tallies and duration.</summary>
    /// <remarks>The consumer recomputes the counts from unit outcomes, so they are informational.</remarks>
    public void Dispose()
    {
        TestLogger.FinishFixture(_name, _testsRun, _testsPassed, _testsFailed, _stopwatch.ElapsedMilliseconds);
    }
}
