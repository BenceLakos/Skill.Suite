using System.Runtime.CompilerServices;
using System.Text;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Captures the event stream in-process by winning the race for <see cref="Console.Out"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>TestLogger</c>'s sink is a <c>static readonly</c> field initialized on first touch of the type,
/// and it captures whatever <see cref="Console.Out"/> is at that moment. A module initializer runs
/// before any test code in this assembly, so redirecting the console here deterministically wins —
/// no production seam, no mutable sink, and it exercises the exact code path a competitor gets when
/// <c>LOG_DIRECTORY</c> is unset.
/// </para>
/// <para>
/// The variable is also cleared, so an inherited <c>LOG_DIRECTORY</c> (running these tests inside a
/// judge container, for instance) cannot silently redirect the events into a file and fail every
/// assertion for the wrong reason. The file sink is covered separately, out of process, by
/// <c>FileSinkTests</c>.
/// </para>
/// </remarks>
internal static class EventCapture
{
    private static readonly StringWriter Buffer = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("LOG_DIRECTORY", null);
        Console.SetOut(Buffer);
    }

    /// <summary>Drops everything captured so far. Call at the start of each test.</summary>
    internal static void Reset() => Buffer.GetStringBuilder().Clear();

    /// <summary>The captured lines, in emission order, with trailing blanks removed.</summary>
    internal static string[] Lines() =>
        Buffer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

    /// <summary>The single captured line. Fails loudly when there is not exactly one.</summary>
    internal static string SingleLine()
    {
        var lines = Lines();
        return lines.Length == 1
            ? lines[0]
            : throw new InvalidOperationException(
                $"Expected exactly one event, captured {lines.Length}:{Environment.NewLine}" +
                string.Join(Environment.NewLine, lines));
    }

    internal static StringBuilder Raw() => Buffer.GetStringBuilder();
}
