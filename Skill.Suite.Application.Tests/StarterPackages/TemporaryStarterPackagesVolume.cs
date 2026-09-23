namespace Skill.Suite.Application.Tests.StarterPackages;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Infra.StarterPackages;

/// <summary>
/// A starter packages root of one test's own, in a real temporary directory that goes when the test does.
/// </summary>
/// <remarks>
/// Real for the reason <see cref="FileSystemStarterPackageStoreTests"/> gives: the behaviour under test is the
/// filesystem's — containment, what a failed write leaves behind, and the modes a script ends up with.
/// </remarks>
internal sealed class TemporaryStarterPackagesVolume : IDisposable
{
    public const long DefaultMaxUploadBytes = 16 * 1024 * 1024;

    public TemporaryStarterPackagesVolume() => Directory.CreateDirectory(Root);

    public string Root { get; } =
        Path.Combine(Path.GetTempPath(), $"skill-suite-starter-packages-{Guid.NewGuid():N}");

    public FileSystemStarterPackageStore CreateStore(long maxUploadBytes = DefaultMaxUploadBytes) =>
        new(Options.Create(new StarterPackagesOptions { RootPath = Root, MaxUploadBytes = maxUploadBytes }),
            NullLogger<FileSystemStarterPackageStore>.Instance);

    /// <summary>Where a path from the root is on disk.</summary>
    public string PathOf(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void CreateFolder(string relativePath) => Directory.CreateDirectory(PathOf(relativePath));

    public async Task WriteAsync(string relativePath, string content)
    {
        var fullPath = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    public Task<string> ReadAsync(string relativePath) => File.ReadAllTextAsync(PathOf(relativePath));

    /// <summary>
    /// Everything directly in a folder, hidden entries included — which is where an upload's temporary file
    /// would show if one were left behind.
    /// </summary>
    public string[] NamesIn(string relativePath) =>
    [
        .. Directory.EnumerateFileSystemEntries(PathOf(relativePath))
            .Select(entry => Path.GetFileName(entry))
            .Order(StringComparer.Ordinal),
    ];

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
