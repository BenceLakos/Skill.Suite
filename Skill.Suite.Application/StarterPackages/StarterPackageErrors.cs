using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages;

public static class StarterPackageErrors
{
    private const long BytesPerMebibyte = 1024 * 1024;

    /// <summary>The configuration key an operator raises the upload limit with.</summary>
    private const string MaxUploadBytesSetting =
        $"{StarterPackagesOptions.SectionName}:{nameof(StarterPackagesOptions.MaxUploadBytes)}";

    public static readonly Error InvalidName =
        Error.Validation("StarterPackage.InvalidName",
            "A package name must start with a letter or digit and may then contain letters, digits, " +
            "'.', '-' and '_' only.");

    public static readonly Error InvalidPath =
        Error.Validation("StarterPackage.InvalidPath",
            "That path leads outside the starter packages directory.");

    public static Error NotFound(string nameOrPath) =>
        Error.NotFound("StarterPackage.NotFound", $"'{nameOrPath}' was not found in the starter packages.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("StarterPackage.AlreadyExists",
            $"A package named '{name}' already exists. Upload it again with replace enabled to overwrite it.");

    public static Error RootUnavailable(string root) =>
        Error.Failure("StarterPackage.RootUnavailable",
            $"The starter packages directory '{root}' does not exist inside the application container. " +
            "It is a docker volume mounted by the compose stack — check that the deployment mounts it.");

    public static Error RootReadOnly(string root) =>
        Error.Failure("StarterPackage.RootReadOnly",
            $"The starter packages directory '{root}' could not be written to. The local development stack " +
            "mounts it read-only, so uploading and deleting only work against the production stack's volume.");

    public static readonly Error EmptyArchive =
        Error.Validation("StarterPackage.EmptyArchive", "The archive contains no files to import.");

    public static Error UnsafeArchiveEntry(string entryName) =>
        Error.Validation("StarterPackage.UnsafeArchiveEntry",
            $"The archive entry '{entryName}' would be written outside the package directory.");

    public static Error ArchiveTooLarge(long limitBytes) =>
        Error.Validation("StarterPackage.ArchiveTooLarge",
            $"The archive unpacks to more than the {limitBytes / BytesPerMebibyte} MiB allowed for one package.");

    public static readonly Error NotAZipArchive =
        Error.Validation("StarterPackage.NotAZipArchive", "The uploaded file is not a readable zip archive.");

    public static readonly Error NotInAPackage =
        Error.Validation("StarterPackage.NotInAPackage",
            "That path is not inside a starter package, and files and folders can only be added or removed " +
            "inside one. Open a package from the packages list first — a new package is created by uploading " +
            "a zip archive.");

    public static Error EntryIsAPackage(string name) =>
        Error.Validation("StarterPackage.EntryIsAPackage",
            $"'{name}' is a whole starter package rather than a file or folder in one. Delete it from the " +
            "packages list instead.");

    public static Error EntryAlreadyExists(string path) =>
        Error.Conflict("StarterPackage.EntryAlreadyExists",
            $"'{path}' already exists. Choose another name, or upload the file again and confirm replacing it.");

    public static Error EntryKindConflict(string path) =>
        Error.Conflict("StarterPackage.EntryKindConflict",
            $"'{path}' cannot be written: a folder is where the file would go, or a file is where one of its " +
            "folders would have to be. Delete whichever is in the way first, or upload into another folder.");

    public static Error ExcludedEntry(string path) =>
        Error.Validation("StarterPackage.ExcludedEntry",
            $"'{path}' is build output, repository metadata or a Finder file, which starter packages leave out " +
            "— an archive import skips it and a download omits it. Leave it out, or choose another name.");

    public static readonly Error InvalidEntryName =
        Error.Validation("StarterPackage.InvalidEntryName",
            "A file or folder name cannot be empty, '.' or '..', cannot contain '/', '\\' or control " +
            $"characters, and can be at most {StarterPackageEntryName.MaxLength} bytes long — that many plain " +
            "letters, fewer with accents. Choose another name.");

    public static Error FileTooLarge(long limitBytes) =>
        Error.Validation("StarterPackage.FileTooLarge",
            $"The file is larger than the {limitBytes / BytesPerMebibyte} MiB allowed for one upload. Leave it " +
            $"out, or raise {MaxUploadBytesSetting} in the application's configuration.");

    public static Error UploadTooLarge(long limitBytes) =>
        Error.Validation("StarterPackage.UploadTooLarge",
            $"These files add up to more than the {limitBytes / BytesPerMebibyte} MiB allowed for one upload. " +
            $"Upload them in smaller batches, or raise {MaxUploadBytesSetting} in the application's " +
            "configuration.");

    /// <summary>
    /// Last resort for a filesystem failure that is neither a missing nor a read-only root — a full volume,
    /// say. The operator sees the underlying message because nothing else here can explain it.
    /// </summary>
    public static Error Failed(string message) =>
        Error.Failure("StarterPackage.Failed", message);
}
