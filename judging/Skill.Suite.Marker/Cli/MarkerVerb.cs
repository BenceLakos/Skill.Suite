namespace Skill.Suite.Marker.Cli;

/// <summary>The subcommand a command line selected.</summary>
public enum MarkerVerb
{
    /// <summary>Compute coverage and mutation scores and append them to the event stream.</summary>
    Score = 0,

    /// <summary>Write the per-aspect yes/no CSV for CIS.</summary>
    Report = 1,

    /// <summary>Print usage.</summary>
    Help = 2,

    /// <summary>Print the tool version.</summary>
    Version = 3,
}
