using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Xunit;
using Xunit.Sdk;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// Per-test logging surface. Wraps xUnit assertions so each one emits an
/// <c>assertion</c> event with expected/actual/passed before the underlying
/// xUnit assertion runs — failing tests still fail.
/// </summary>
public sealed class TestLog
{
    // Tracks the active per-test log so CallLoggingProxy can find it without
    // an explicit reference. AsyncLocal flows through xUnit's per-test context,
    // so parallel test classes don't see each other's logs.
    private static readonly AsyncLocal<TestLog?> CurrentLocal = new();
    internal static TestLog? Current => CurrentLocal.Value;
    internal static void SetCurrent(TestLog? log) => CurrentLocal.Value = log;

    // FirstChanceException fires the instant any exception is thrown in this
    // AppDomain, before any catch handler sees it. xUnit catches test-method
    // exceptions outside our reach, so we can't read its verdict from Dispose —
    // but FirstChance lets us observe the throw ourselves and stash it on the
    // active TestLog. Cleared on every "observed" event (assertion ran, proxy
    // call returned cleanly), so SUT-internal try/catch doesn't false-flag.
    static TestLog()
    {
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        var current = CurrentLocal.Value;
        if (current is null) return;
        // Our own assertion failures route through Run() — don't double-track.
        if (e.Exception is XunitException) return;
        // Reflection wraps any throw from an invoked method in a fresh
        // TargetInvocationException, which fires FirstChance *after* the
        // inner exception we actually care about. Ignoring the wrapper
        // keeps the diagnostic pointing at the real cause (e.g. an NRE
        // in the test body, not the reflection plumbing).
        if (e.Exception is System.Reflection.TargetInvocationException) return;
        current._lastUnobservedException = e.Exception;
    }

    internal string Fixture { get; }
    internal string Test { get; }
    internal bool Failed { get; private set; }
    internal string? FirstFailure { get; private set; }

    // Tracks the most recent service-under-test exception that the proxy
    // has seen. Cleared when any assertion runs (assertion observed = the
    // throw was handled). If still set at Dispose time, the test exited
    // via an unhandled exception and outcome must be "failed", not "passed".
    private Exception? _pendingServiceThrow;
    internal bool HasUnhandledServiceThrow => _pendingServiceThrow is not null;
    internal string? UnhandledServiceThrowDescription =>
        _pendingServiceThrow is null
            ? null
            : $"unhandled {_pendingServiceThrow.GetType().Name}: {_pendingServiceThrow.Message}";

    internal void MarkServiceThrew(Exception ex) => _pendingServiceThrow = ex;

    // Most recent exception observed via FirstChanceException whose handler
    // hasn't been "consumed" by a subsequent assertion or clean proxy return.
    // If this is still set at Dispose, the test method exited via an
    // exception that xUnit will fail on but our flag-based tracking would
    // otherwise miss (e.g. an NRE in the test body on a partially-null
    // return that slipped past the proxy contract check).
    private Exception? _lastUnobservedException;
    internal bool HasUnobservedException => _lastUnobservedException is not null;
    internal string? UnobservedExceptionDescription =>
        _lastUnobservedException is null
            ? null
            : $"unhandled {_lastUnobservedException.GetType().Name}: {_lastUnobservedException.Message}";

    // Called by the proxy after a proxied call returns without throwing — proves
    // any SUT-internal throw+catch was indeed caught (not a test failure).
    internal void MarkCallObserved() => _lastUnobservedException = null;

    internal TestLog(string fixture, string test)
    {
        Fixture = fixture;
        Test = test;
    }

    // -------- Call logging --------

    /// <summary>Logs a call into the system under test that returns nothing.</summary>
    /// <param name="target">Called member, conventionally <c>IInterface.Method</c>.</param>
    /// <param name="arguments">Arguments passed to the call.</param>
    public void Call(string target, params object?[] arguments)
    {
        TestLogger.Call(Fixture, Test, target, arguments);
    }

    /// <summary>Logs a call into the system under test and passes its result straight through.</summary>
    /// <typeparam name="T">Return type of the call.</typeparam>
    /// <param name="target">Called member, conventionally <c>IInterface.Method</c>.</param>
    /// <param name="returned">The value the call returned; also the value this method returns.</param>
    /// <param name="arguments">Arguments passed to the call.</param>
    /// <returns><paramref name="returned"/>, so the call can be logged inline.</returns>
    public T CallReturning<T>(string target, T returned, params object?[] arguments)
    {
        TestLogger.Call(Fixture, Test, target, arguments, returned, hasReturned: true);
        return returned;
    }

    // -------- Assertion wrappers --------

    /// <summary>Asserts two values are equal.</summary>
    /// <typeparam name="T">Type being compared.</typeparam>
    /// <param name="expected">Expected value.</param>
    /// <param name="actual">Actual value.</param>
    public void AssertEqual<T>(T expected, T actual) =>
        Run("equal", expected, actual, () => Assert.Equal(expected, actual));

    /// <summary>Asserts two doubles are equal within an absolute tolerance.</summary>
    /// <param name="expected">Expected value.</param>
    /// <param name="actual">Actual value.</param>
    /// <param name="tolerance">Largest accepted absolute difference, for example <c>0.0001</c>.</param>
    public void AssertEqual(double expected, double actual, double tolerance) =>
        Run("equal", expected, actual, () => Assert.Equal(expected, actual, tolerance));

    /// <summary>Asserts two doubles are equal when rounded to a number of decimal places.</summary>
    /// <param name="expected">Expected value.</param>
    /// <param name="actual">Actual value.</param>
    /// <param name="precision">Decimal places to round both values to before comparing.</param>
    /// <remarks>
    /// This overload exists to close a trap: with only the tolerance overload in scope,
    /// <c>AssertEqual(1.0, 1.004, 3)</c> converts the <c>3</c> to a tolerance of <c>3.0</c> and accepts
    /// almost anything, while reading as three-decimal-strict. Keep both so the literal binds to the
    /// overload it looks like.
    /// </remarks>
    public void AssertEqual(double expected, double actual, int precision) =>
        Run("equal", expected, actual, () => Assert.Equal(expected, actual, precision));

    /// <summary>Asserts two values are not equal.</summary>
    /// <typeparam name="T">Type being compared.</typeparam>
    /// <param name="expected">Value the actual must differ from.</param>
    /// <param name="actual">Actual value.</param>
    public void AssertNotEqual<T>(T expected, T actual) =>
        Run("not-equal", expected, actual, () => Assert.NotEqual(expected, actual));

    /// <summary>Asserts a condition holds.</summary>
    /// <param name="condition">Condition expected to be <see langword="true"/>.</param>
    public void AssertTrue(bool condition) =>
        Run("true", true, condition, () => Assert.True(condition));

    /// <summary>Asserts a condition does not hold.</summary>
    /// <param name="condition">Condition expected to be <see langword="false"/>.</param>
    public void AssertFalse(bool condition) =>
        Run("false", false, condition, () => Assert.False(condition));

    /// <summary>Asserts a value is null.</summary>
    /// <param name="value">Value expected to be <see langword="null"/>.</param>
    public void AssertNull(object? value) =>
        Run("null", null, value, () => Assert.Null(value));

    // CS8777: the non-null guarantee does hold - Assert.NotNull throws otherwise - but the assert runs
    // inside a lambda, so the compiler cannot see it and flow analysis for callers still benefits from
    // the [NotNull] annotation. Suppressed rather than dropping the annotation.
#pragma warning disable CS8777
    /// <summary>Asserts a value is not null, and narrows it to non-null for the caller.</summary>
    /// <param name="value">Value expected to be non-null.</param>
    public void AssertNotNull([NotNull] object? value) =>
        Run("not-null", "<non-null>", value, () => Assert.NotNull(value));
#pragma warning restore CS8777

    /// <summary>Asserts two references point at the same instance.</summary>
    /// <param name="expected">Expected instance.</param>
    /// <param name="actual">Actual instance.</param>
    public void AssertSame(object? expected, object? actual) =>
        Run("same", expected, actual, () => Assert.Same(expected!, actual!));

    /// <summary>Asserts two references point at different instances.</summary>
    /// <param name="expected">Instance the actual must not be.</param>
    /// <param name="actual">Actual instance.</param>
    public void AssertNotSame(object? expected, object? actual) =>
        Run("not-same", expected, actual, () => Assert.NotSame(expected!, actual!));

    /// <summary>Asserts a collection is empty.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="collection">Collection expected to contain nothing.</param>
    public void AssertEmpty<T>(IEnumerable<T> collection) =>
        Run("empty", "<empty>", collection, () => Assert.Empty(collection));

    /// <summary>Asserts a collection contains at least one element.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="collection">Collection expected to be non-empty.</param>
    public void AssertNotEmpty<T>(IEnumerable<T> collection) =>
        Run("not-empty", "<non-empty>", collection, () => Assert.NotEmpty(collection));

    /// <summary>Asserts a collection holds exactly one element, and returns it.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="collection">Collection expected to hold one element.</param>
    /// <returns>The single element.</returns>
    public T AssertSingle<T>(IEnumerable<T> collection)
    {
        T? captured = default;
        Run("single", "<single element>", collection, () => captured = Assert.Single(collection));
        return captured!;
    }

    /// <summary>Asserts a collection contains a value.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="expected">Value expected to be present.</param>
    /// <param name="collection">Collection to search.</param>
    public void AssertContains<T>(T expected, IEnumerable<T> collection) =>
        Run("contains", expected, collection, () => Assert.Contains(expected, collection));

    /// <summary>Asserts a string contains a substring.</summary>
    /// <param name="expected">Substring expected to be present.</param>
    /// <param name="actual">String to search.</param>
    public void AssertContains(string expected, string actual) =>
        Run("contains", expected, actual, () => Assert.Contains(expected, actual));

    /// <summary>Asserts an action throws a given exception type, and returns the exception.</summary>
    /// <typeparam name="TException">Expected exception type; a derived type also satisfies it.</typeparam>
    /// <param name="action">Action expected to throw.</param>
    /// <returns>The thrown exception.</returns>
    public TException AssertThrows<TException>(Action action) where TException : Exception
    {
        Exception? thrown = null;
        try { action(); }
        catch (Exception ex) { thrown = ex; }

        var actualLabel = thrown?.GetType().Name ?? "<no exception>";
        var passed = thrown is TException;
        TestLogger.Assertion(Fixture, Test, "throws", typeof(TException).Name, actualLabel, passed);
        // AssertThrows is an assertion: it observed and consumed any service throw.
        _pendingServiceThrow = null;
        _lastUnobservedException = null;

        if (passed) return (TException)thrown!;

        Failed = true;
        FirstFailure ??= $"Expected {typeof(TException).Name}, got {actualLabel}";
        throw new XunitException(
            thrown is null
                ? $"Expected {typeof(TException).Name} to be thrown; no exception was thrown."
                : $"Expected {typeof(TException).Name}, got {thrown.GetType().Name}: {thrown.Message}");
    }

    private void Run(string kind, object? expected, object? actual, Action assert)
    {
        XunitException? failure = null;
        try { assert(); }
        catch (XunitException ex) { failure = ex; }

        var passed = failure is null;
        TestLogger.Assertion(Fixture, Test, kind, expected, actual, passed);
        // Reaching an assertion proves the test flow handled (or never saw)
        // any earlier service-under-test exception.
        _pendingServiceThrow = null;
        _lastUnobservedException = null;

        if (passed) return;

        Failed = true;
        FirstFailure ??= failure!.Message;
        throw failure!;
    }
}
