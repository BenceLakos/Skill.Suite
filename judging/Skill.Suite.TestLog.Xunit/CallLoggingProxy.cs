using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Skill.Suite.TestLog.Xunit;

/// <summary>
/// Transparent proxy that intercepts every interface method call on a wrapped
/// service and emits a <c>call</c> event via <see cref="TestLogger"/>.
/// The proxy reads the current test context from <see cref="TestLog.Current"/>
/// at invocation time, so it can be constructed during field initialization
/// — before the <see cref="LoggedTest{TSelf}"/> constructor has set up
/// per-test context.
/// </summary>
public class CallLoggingProxy<T> : DispatchProxy where T : class
{
    private T _target = null!;
    private string _targetName = "";

    /// <summary>Wraps <paramref name="target"/> in a logging proxy.</summary>
    public static T Wrap(T target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (!typeof(T).IsInterface)
        {
            throw new InvalidOperationException(
                $"CallLoggingProxy<{typeof(T).Name}>: T must be an interface.");
        }

        var proxy = Create<T, CallLoggingProxy<T>>();
        var inner = (CallLoggingProxy<T>)(object)proxy!;
        inner._target = target;
        inner._targetName = typeof(T).Name;
        return proxy!;
    }

    /// <summary>Intercepts one interface call, logs it, and forwards it to the wrapped service.</summary>
    /// <param name="method">The interface method being called; <see langword="null"/> is ignored.</param>
    /// <param name="args">Call arguments. <see cref="Stream"/> arguments are snapshotted for logging.</param>
    /// <returns>Whatever the wrapped service returned.</returns>
    /// <remarks>
    /// When no test context is current (<see cref="TestLog.Current"/> is null) the call still executes
    /// and exceptions still propagate — only the logging is skipped.
    /// </remarks>
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null) return null;

        var arguments = args ?? Array.Empty<object?>();
        var fullTarget = $"{_targetName}.{method.Name}";
        var log = TestLog.Current;

        // Streams need special handling: System.Text.Json can't serialise them
        // (the `arguments` array would carry a null placeholder), and reading
        // the stream for logging would drain it before the service sees it.
        // Snapshot the bytes, build a fresh MemoryStream for the actual call,
        // and record a human-readable summary in the log.
        var loggedArgs = new object?[arguments.Length];
        var callArgs = new object?[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            (loggedArgs[i], callArgs[i]) = PrepareArg(arguments[i]);
        }

        object? returned = null;
        bool hasReturned = false;
        Exception? thrown = null;

        try
        {
            returned = method.Invoke(_target, callArgs);
            hasReturned = method.ReturnType != typeof(void);

            // Contract check: methods whose declared return type is non-nullable
            // must not return null. Without this, a service that "stubs" by
            // returning null would let every subsequent property access on the
            // return value throw NRE in the test body — and our outcome
            // tracking (which only sees service throws) would happily call
            // the test "passed".
            if (hasReturned && returned is null && IsNonNullableReturn(method))
            {
                thrown = new InvalidOperationException(
                    $"{fullTarget} returned null but its declared return type " +
                    $"{method.ReturnType.Name} is non-nullable.");
            }
        }
        catch (TargetInvocationException tie)
        {
            thrown = tie.InnerException ?? tie;
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        if (log is not null)
        {
            TestLogger.Call(
                log.Fixture, log.Test, fullTarget,
                loggedArgs,
                returned, hasReturned,
                thrown?.GetType().Name);

            // Tell the per-test log that the service threw. If no assertion
            // is reached afterward to "consume" the throw, Dispose will treat
            // the test as failed — otherwise the JSON outcome would falsely
            // claim "passed" because no Assert.X actually failed.
            if (thrown is not null) log.MarkServiceThrew(thrown);
            // A clean call also "consumes" any FirstChance exception that
            // fired during the invocation (= SUT threw+caught internally).
            // Without this, the FirstChance stash would survive into Dispose
            // and false-flag the test.
            else log.MarkCallObserved();
        }

        // ExceptionDispatchInfo rather than `throw thrown;`: a bare rethrow resets the stack trace, so
        // the failure would point here instead of at the line inside the service that actually threw.
        // Verdict-neutral — MarkServiceThrew already ran, and the rethrow still raises FirstChanceException.
        if (thrown is not null) ExceptionDispatchInfo.Capture(thrown).Throw();
        return returned;
    }

    // Reads the compiler-emitted nullability annotation on the return parameter.
    // Value types: non-nullable unless they are Nullable<T>. Reference types:
    // depends on the #nullable context the method was compiled under — if the
    // declared type is e.g. `Report` (not `Report?`), ReadState is NotNull
    // and a null return is a contract break.
    //
    // Note this reads the *interface* method: DispatchProxy hands us the interface MethodInfo, so the
    // annotation comes from the Contracts assembly, not the implementation. A Contracts project built
    // without <Nullable>enable</Nullable> silently disables this whole check.
    private static bool IsNonNullableReturn(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (returnType.IsValueType)
        {
            return Nullable.GetUnderlyingType(returnType) is null;
        }

        try
        {
            var ctx = new NullabilityInfoContext();
            var info = ctx.Create(method.ReturnParameter);
            return info.ReadState == NullabilityState.NotNull;
        }
        catch
        {
            // If the runtime can't read the metadata (older TFM, obfuscation,
            // etc.) we'd rather under-report than spuriously fail tests.
            return false;
        }
    }

    private static (object? loggedArg, object? callArg) PrepareArg(object? arg)
    {
        if (arg is not Stream stream || !stream.CanRead) return (arg, arg);

        byte[] bytes;
        try
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        catch
        {
            // Couldn't read; fall back to a placeholder and leave the original
            // stream for the service to deal with.
            return (new Dictionary<string, object?>
            {
                ["$stream"] = true,
                ["error"] = "unreadable",
            }, arg);
        }

        var logged = new Dictionary<string, object?>
        {
            ["$stream"] = true,
            ["length"] = bytes.Length,
            ["content"] = TryDecodeAsText(bytes, out var text)
                ? text
                : new Dictionary<string, object?>
                {
                    ["binary"] = true,
                    ["preview_base64"] = Convert.ToBase64String(bytes, 0, Math.Min(bytes.Length, 256)),
                },
        };

        // Hand a fresh stream to the call so the service still reads the same bytes.
        return (logged, new MemoryStream(bytes, writable: false));
    }

    private static bool TryDecodeAsText(byte[] bytes, out string text)
    {
        text = string.Empty;
        if (bytes.Length == 0) return true;

        // Refuse early if the buffer contains a NUL byte — almost certainly not text.
        for (var i = 0; i < Math.Min(bytes.Length, 4096); i++)
        {
            if (bytes[i] == 0) return false;
        }

        try
        {
            // Strict UTF-8 — throws on invalid byte sequences.
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Sugar for <see cref="CallLoggingProxy{T}.Wrap"/>: <c>service.WithCallLogging()</c>.
/// </summary>
public static class CallLoggingExtensions
{
    /// <summary>Wraps a resolved service so every call on it is logged.</summary>
    /// <typeparam name="T">The contract interface. Must be an interface.</typeparam>
    /// <param name="target">The service instance to wrap.</param>
    /// <returns>A proxy that logs each call and forwards it to <paramref name="target"/>.</returns>
    public static T WithCallLogging<T>(this T target) where T : class
        => CallLoggingProxy<T>.Wrap(target);
}
