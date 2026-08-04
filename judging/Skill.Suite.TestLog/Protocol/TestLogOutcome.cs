namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// A unit test's verdict, as carried by <c>finish-unit-test</c>.
/// </summary>
/// <remarks>
/// Named <c>TestLogOutcome</c> rather than <c>TestOutcome</c> because these sources are compiled into
/// <c>Skill.Suite.Application</c>, where <c>Skill.Suite.Domain.TestRuns.TestOutcome</c> already exists — the
/// shorter name would be genuinely ambiguous inside the parser that maps between them.
/// <para>
/// The wire spelling is lowercase, produced by the camelCase policy the enum converter is configured with in
/// <see cref="EventJson"/>. Reading goes through <see cref="Outcomes.Parse"/>, which is more forgiving still.
/// </para>
/// </remarks>
public enum TestLogOutcome
{
    /// <summary>No verdict could be read. Never emitted; only produced by the reader.</summary>
    Unknown = 0,

    /// <summary>The test passed.</summary>
    Passed = 1,

    /// <summary>An assertion failed.</summary>
    Failed = 2,

    /// <summary>The test did not run.</summary>
    Skipped = 3,

    /// <summary>The test threw rather than asserting.</summary>
    Errored = 4,
}
