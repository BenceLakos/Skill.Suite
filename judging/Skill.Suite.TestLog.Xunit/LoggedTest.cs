using Skill.Suite.TestLog.Protocol;
using System.Diagnostics;
using System.Reflection;
using Xunit.Abstractions;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// Base class for test classes that emit structured JSON logs.
/// Combine with <c>IClassFixture&lt;FixtureScope&lt;TSelf&gt;&gt;</c> on the
/// derived class for full fixture lifecycle coverage.
/// </summary>
/// <typeparam name="TSelf">The concrete test class type — used to name the fixture.</typeparam>
public abstract class LoggedTest<TSelf> : IDisposable
{
    private readonly FixtureScope<TSelf> _scope;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string? _aspect;
    private readonly bool _aspectCompetitorVisible;

    /// <summary>Per-test logging surface: <c>Log.AssertEqual(...)</c>, <c>Log.Call(...)</c>, etc.</summary>
    protected TestLog Log { get; }

    /// <summary>Starts a unit test: resolves its name and aspect, then emits <c>start-unit-test</c>.</summary>
    /// <param name="output">xUnit's output helper, from which the running test is discovered.</param>
    /// <param name="scope">The class fixture bracketing this test class.</param>
    protected LoggedTest(ITestOutputHelper output, FixtureScope<TSelf> scope)
    {
        _scope = scope;
        var fixture = typeof(TSelf).Name;
        var test = ExtractTestDisplayName(output);
        var aspect = ResolveAspect(test);
        _aspect = aspect?.Id;
        _aspectCompetitorVisible = aspect?.CompetitorVisible ?? false;
        Log = new TestLog(fixture, test);
        // Make this log discoverable by CallLoggingProxy on the same async context.
        TestLog.SetCurrent(Log);
        TestLogger.StartUnitTest(fixture, test, _aspect, _aspectCompetitorVisible);
    }

    /// <summary>Reconstructs the verdict, emits <c>finish-unit-test</c>, and clears the test context.</summary>
    public void Dispose()
    {
        // "Passed" means: no assertion failed AND no exception escaped
        // unhandled. We track three independent signals because xUnit catches
        // the test-method throw outside our reach and doesn't pass it to
        // Dispose, so we have to reconstruct the verdict ourselves:
        //   - Log.Failed: an assertion wrapper caught a XunitException.
        //   - HasUnhandledServiceThrow: the proxy caught a throw from a
        //     proxied call (or synthesized one for a null-return contract
        //     violation) and no later assertion consumed it.
        //   - HasUnobservedException: FirstChanceException observed a throw
        //     that wasn't subsequently cleared by an assertion or a clean
        //     proxy return — covers test-body NREs and similar.
        // Any one of these tripped → xUnit also saw a failure → our verdict
        // must agree.
        var passed = !Log.Failed
            && !Log.HasUnhandledServiceThrow
            && !Log.HasUnobservedException;
        var error = Log.FirstFailure
            ?? Log.UnhandledServiceThrowDescription
            ?? Log.UnobservedExceptionDescription;

        // Only ever Passed or Failed: xUnit handles skipping before the constructor runs, and an errored test
        // is reported as failed here because the `call` event carrying `threw` is what promotes it to errored
        // on the consumer side.
        TestLogger.FinishUnitTest(
            Log.Fixture, Log.Test,
            passed ? TestLogOutcome.Passed : TestLogOutcome.Failed,
            _stopwatch.ElapsedMilliseconds,
            passed ? null : error,
            _aspect,
            _aspectCompetitorVisible);
        _scope.RecordTest(passed);
        TestLog.SetCurrent(null);
    }

    // xUnit 2.x TestOutputHelper exposes the running ITest only via a private field.
    private static string ExtractTestDisplayName(ITestOutputHelper output)
    {
        var field = output.GetType().GetField("test", BindingFlags.NonPublic | BindingFlags.Instance);
        var test = field?.GetValue(output);
        var displayName = test?.GetType().GetProperty("DisplayName")?.GetValue(test) as string;
        if (string.IsNullOrEmpty(displayName)) return "unknown";

        // Strip the "Namespace.Class." prefix, preserving theory args after the parens.
        // e.g. "A.B.C.MethodName(arg: 1)" → "MethodName(arg: 1)"
        var openParen = displayName.IndexOf('(');
        var qualifier = openParen < 0 ? displayName : displayName[..openParen];
        var lastDot = qualifier.LastIndexOf('.');
        return lastDot < 0 ? displayName : displayName[(lastDot + 1)..];
    }

    /// <summary>
    /// Finds the aspect this test case belongs to: the method's own <see cref="AspectAttribute"/> when
    /// it has one, otherwise the test class's, otherwise none — an ungraded test.
    /// </summary>
    /// <remarks>
    /// A method-level attribute replaces a class-level one rather than combining with it: one test case
    /// belongs to exactly one aspect, because that is the unit grading works in. Resolution is by method
    /// name off <typeparamref name="TSelf"/>, since the display name may carry theory arguments and every
    /// data row of a theory shares its method's aspect.
    /// </remarks>
    private static AspectAttribute? ResolveAspect(string testDisplayName) =>
        FindTestMethod(testDisplayName)?.GetCustomAttribute<AspectAttribute>(inherit: true)
        ?? typeof(TSelf).GetCustomAttribute<AspectAttribute>(inherit: true);

    private static MethodInfo? FindTestMethod(string testDisplayName)
    {
        var openParen = testDisplayName.IndexOf('(');
        var name = openParen < 0 ? testDisplayName : testDisplayName[..openParen];
        if (string.IsNullOrEmpty(name)) return null;

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        // Array.Find over GetMethods rather than GetMethod: an overloaded test method would make
        // GetMethod throw AmbiguousMatchException, and overloads share a name so they share an aspect.
        return Array.Find(typeof(TSelf).GetMethods(Flags), m => m.Name == name);
    }
}
