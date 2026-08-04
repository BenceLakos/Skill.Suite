namespace Skill.Suite.Application.TestRuns.ExecuteTestRun;

/// <summary>
/// The lines read from a judgement container's event log, and whether the log was longer than the ingest limit.
/// </summary>
/// <param name="Lines">The lines that were read, in file order.</param>
/// <param name="Truncated">
/// True when the log exceeded <see cref="EventLogReader.MaxLines"/> or <see cref="EventLogReader.MaxCharacters"/>
/// and the remainder was dropped. Carried out rather than only logged, so the run can record a diagnostic saying
/// its results are incomplete instead of silently looking short.
/// </param>
public sealed record EventLogContent(IReadOnlyList<string> Lines, bool Truncated);
