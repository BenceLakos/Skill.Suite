namespace Skill.Suite.Application.StarterPackages;

/// <summary>One file an upload intends to write, as the browser describes it before sending any bytes.</summary>
/// <param name="RelativePath">
/// From the folder being uploaded into, always separated by <c>/</c>: the file's name, or its path inside an
/// uploaded folder.
/// </param>
/// <param name="SizeBytes">
/// The size the browser declares — enough to plan with; the write itself counts what actually arrives.
/// </param>
public sealed record StarterPackageUploadFileDto(string RelativePath, long SizeBytes);
