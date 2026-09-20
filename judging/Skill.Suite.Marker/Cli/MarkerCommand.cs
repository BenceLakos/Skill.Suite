namespace Skill.Suite.Marker.Cli;

/// <summary>
/// A parsed command line.
/// </summary>
/// <param name="Verb">Which subcommand to run.</param>
/// <param name="MapPath">Path to <c>marking-map.json</c>.</param>
/// <param name="EventsPath">Path to the <c>events.jsonl</c> to read and, for <c>score</c>, append to.</param>
/// <param name="TrxPaths">TRX result files.</param>
/// <param name="CoveragePaths">Cobertura coverage files.</param>
/// <param name="MutationPath">Stryker JSON report.</param>
/// <param name="OutPath">Where <c>report</c> writes its CSV.</param>
/// <param name="FixtureCoveragePath">
/// Directory whose immediate subdirectories are named after test classes, each holding that class's own
/// Cobertura report. Optional: without it the run simply emits no fixture-scoped coverage.
/// </param>
public sealed record MarkerCommand(
    MarkerVerb Verb,
    string MapPath = "",
    string EventsPath = "",
    IReadOnlyList<string>? TrxPaths = null,
    IReadOnlyList<string>? CoveragePaths = null,
    string? MutationPath = null,
    string OutPath = "",
    string? FixtureCoveragePath = null)
{
    /// <summary>TRX files, never null.</summary>
    public IReadOnlyList<string> Trx => TrxPaths ?? [];

    /// <summary>Coverage files, never null.</summary>
    public IReadOnlyList<string> Coverage => CoveragePaths ?? [];
}
