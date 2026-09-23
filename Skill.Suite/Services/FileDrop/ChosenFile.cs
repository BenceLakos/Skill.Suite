namespace Skill.Suite.Services.FileDrop;

/// <summary>One file of a <see cref="FileDropBatch"/>, as the browser describes it.</summary>
/// <param name="Index">Its place in the batch, which is what the browser is asked for its bytes by.</param>
/// <param name="RelativePath">
/// Separated by <c>/</c>: the file's name, or its path inside a chosen or dropped folder, that folder's own
/// name first.
/// </param>
/// <param name="SizeBytes">
/// The size the browser reports for it, fixed when it was chosen; an empty file is never read at all.
/// </param>
public sealed record ChosenFile(int Index, string RelativePath, long SizeBytes);
