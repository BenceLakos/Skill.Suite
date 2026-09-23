namespace Skill.Suite.Components.Pages.StarterPackages;

/// <summary>
/// One batch being uploaded into a folder of a package, counted as it goes: what the progress bar shows while
/// it runs, and what the summary and the list of problems say once it is over.
/// </summary>
/// <param name="folderPath">The folder uploaded into, from the starter packages root.</param>
public sealed class StarterPackageUploadRun(string folderPath)
{
    public string FolderPath { get; } = folderPath;

    /// <summary>
    /// Whether the batch got past planning and the question about replacing files. Until then nothing has
    /// been written and nothing counted, so there is nothing to sum up either.
    /// </summary>
    public bool Started { get; set; }

    /// <summary>Files to write: the new ones, and the existing ones the administrator chose to replace.</summary>
    public int Total { get; set; }

    /// <summary>Files of <see cref="Total"/> dealt with so far, written or not.</summary>
    public int Completed { get; set; }

    /// <summary>The file being written now, from <see cref="FolderPath"/>.</summary>
    public string? Current { get; set; }

    public int Uploaded { get; set; }

    /// <summary>Of <see cref="Uploaded"/>, how many overwrote a file that was already there.</summary>
    public int Replaced { get; set; }

    /// <summary>Existing files the administrator chose to keep rather than replace.</summary>
    public int Skipped { get; set; }

    /// <summary>Build output, repository metadata and Finder files, which a package never takes.</summary>
    public int Excluded { get; set; }

    /// <summary>Stopped by the administrator, or by leaving the page, before every file was dealt with.</summary>
    public bool Cancelled { get; set; }

    /// <summary>Stopped by a failure that every file after it would have run into as well.</summary>
    public bool Stopped { get; set; }

    public List<StarterPackageUploadProblem> Problems { get; } = [];
}
