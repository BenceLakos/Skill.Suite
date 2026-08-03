namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Base for tests that read the captured event stream: clears the buffer before each test.
/// </summary>
/// <remarks>
/// The capture buffer is process-wide static state, so these tests must not run concurrently.
/// <c>AssemblyInfo.cs</c> disables parallelization for the whole assembly.
/// </remarks>
public abstract class EventFixture
{
    protected EventFixture() => EventCapture.Reset();

    /// <summary>Captured lines with timestamp values replaced, so goldens are stable.</summary>
    protected static string[] NormalizedLines() =>
        EventCapture.Lines().Select(EventNormalizer.Normalize).ToArray();

    /// <summary>The single captured line, timestamp normalized.</summary>
    protected static string NormalizedLine() => EventNormalizer.Normalize(EventCapture.SingleLine());

    /// <summary>The single captured line, exactly as written.</summary>
    protected static string RawLine() => EventCapture.SingleLine();
}
