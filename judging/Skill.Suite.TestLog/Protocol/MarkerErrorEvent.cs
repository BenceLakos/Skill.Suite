namespace Skill.Suite.TestLog.Protocol;

/// <summary>A fatal judge diagnostic: why a run could not complete, as opposed to how a submission scored.</summary>
/// <param name="Detail">One-line human-readable cause.</param>
/// <remarks>
/// This is the only event also written by hand, in bash, by <c>judge-lib.sh</c> — the judge image ships neither
/// <c>jq</c> nor <c>python3</c>, so the shell composes the JSON itself. Its shape must therefore stay exactly
/// this simple: a timestamp, the discriminator, and one string.
/// </remarks>
public sealed record MarkerErrorEvent(
    string? Detail)
    : TestLogEvent;
