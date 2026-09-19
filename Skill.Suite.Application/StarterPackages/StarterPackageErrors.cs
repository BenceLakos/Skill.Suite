using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages;

public static class StarterPackageErrors
{
    private const long BytesPerMebibyte = 1024 * 1024;

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

    /// <summary>
    /// Last resort for a filesystem failure that is neither a missing nor a read-only root — a full volume,
    /// say. The operator sees the underlying message because nothing else here can explain it.
    /// </summary>
    public static Error Failed(string message) =>
        Error.Failure("StarterPackage.Failed", message);
}
