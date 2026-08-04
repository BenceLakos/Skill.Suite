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
public sealed record MarkerCommand(
    MarkerVerb Verb,
    string MapPath = "",
    string EventsPath = "",
    IReadOnlyList<string>? TrxPaths = null,
    IReadOnlyList<string>? CoveragePaths = null,
    string? MutationPath = null,
    string OutPath = "")
{
    /// <summary>TRX files, never null.</summary>
    public IReadOnlyList<string> Trx => TrxPaths ?? [];

    /// <summary>Coverage files, never null.</summary>
    public IReadOnlyList<string> Coverage => CoveragePaths ?? [];
}
