namespace Skill.Suite.Application.StarterPackages;

public sealed class StarterPackagesOptions
{
    public const string SectionName = "StarterPackages";

    /// <summary>
    /// Directory holding one top-level folder per starter package, e.g.
    /// <c>/starter-packages/fibonacci-session</c>.
    /// </summary>
    /// <remarks>
    /// Backed by a docker volume rather than a host directory, so the deployment is identical on a Linux,
    /// macOS or Windows host and a session's <c>TemplateFolder</c> is a container path the application can
    /// always read. The local development stack mounts <c>judging/samples</c> here read-only, so uploads and
    /// deletes fail there by design — see <see cref="StarterPackageErrors.RootReadOnly"/>.
    /// </remarks>
    public string RootPath { get; set; } = "/starter-packages";

    /// <summary>
    /// Largest uncompressed archive accepted by an upload, and the most a file or folder uploaded into a
    /// package may come to.
    /// </summary>
    /// <remarks>
    /// Applied to the sum of the entries' declared sizes before anything is written, so a zip bomb is
    /// rejected rather than extracted. It also bounds what the browser is allowed to stream over the
    /// Blazor circuit in the first place.
    /// <para>
    /// A file or folder upload is held to it twice: planning refuses a batch whose files add up to more, and
    /// each file is refused once more than this many of its bytes have arrived — the plan only has the sizes
    /// the browser declares, while the write counts what actually comes.
    /// </para>
    /// </remarks>
    public long MaxUploadBytes { get; set; } = 512L * 1024 * 1024;
}
