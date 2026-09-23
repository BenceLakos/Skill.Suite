namespace Skill.Suite.Application.StarterPackages;

/// <summary>What uploading one file into a package would do, decided before any of its bytes are sent.</summary>
public enum StarterPackageUploadAction
{
    /// <summary>Nothing is there yet: the file is created, along with any folder missing on the way to it.</summary>
    Create,

    /// <summary>A file is already there and would be overwritten, which the administrator confirms first.</summary>
    Replace,

    /// <summary>
    /// Build output, repository metadata or a Finder file — never written, by the same rule an archive
    /// import and a download apply.
    /// </summary>
    Excluded,

    /// <summary>
    /// Cannot be written at all: a folder is where the file would go, or a file is where one of its folders
    /// would have to be.
    /// </summary>
    Blocked,
}
