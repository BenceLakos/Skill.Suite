namespace Skill.Suite.Application.StarterPackages;

/// <summary>A prepared download: what to call the file, and how to write it.</summary>
/// <remarks>
/// The content is a callback rather than a stream so a folder can be zipped straight into the response body
/// instead of being buffered first — a package is a source tree, and the endpoint should not need a copy of
/// it in memory to serve it.
/// </remarks>
public sealed record StarterPackageDownload(
    string FileName,
    string ContentType,
    Func<Stream, CancellationToken, Task> WriteToAsync);
