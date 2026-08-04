namespace Skill.Suite.Domain.TestRuns;

/// <summary>
/// Size limits applied to everything ingested from a judgement image, and the single source of truth for
/// the matching database column widths.
/// </summary>
/// <remarks>
/// The event stream is written by code running inside a competitor's submission, so its field lengths are
/// not under our control. Truncating at ingest is deliberate: one over-long name would otherwise throw on
/// <c>SaveChanges</c> and lose the results of the entire run, not just that one row. The trade is that two
/// names differing only past the limit collapse into one — vastly better than losing the run.
/// </remarks>
public static class TestRunLimits
{
    /// <summary>Maximum stored length of a fixture, test or part name.</summary>
    public const int NameMaxLength = 500;

    /// <summary>Maximum stored length of a diagnostic detail.</summary>
    public const int DetailMaxLength = 4_000;

    /// <summary>Maximum stored length of an aspect id.</summary>
    public const int AspectMaxLength = 100;

    /// <summary>Truncates a name to <see cref="NameMaxLength"/>.</summary>
    public static string TruncateName(string value) => Truncate(value, NameMaxLength);

    /// <summary>Truncates a diagnostic detail to <see cref="DetailMaxLength"/>.</summary>
    public static string TruncateDetail(string value) => Truncate(value, DetailMaxLength);

    /// <summary>Truncates an aspect id to <see cref="AspectMaxLength"/>, preserving null.</summary>
    public static string? TruncateAspect(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value, AspectMaxLength);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
