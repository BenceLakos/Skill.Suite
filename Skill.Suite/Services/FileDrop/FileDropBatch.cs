namespace Skill.Suite.Services.FileDrop;

/// <summary>The files one pick or one drop handed over, held by the browser until the batch is released.</summary>
/// <param name="Id">What the browser knows the batch by, for reading its files and for releasing it.</param>
/// <param name="Files">In the order they were chosen; empty when the selection held no files.</param>
public sealed record FileDropBatch(int Id, IReadOnlyList<ChosenFile> Files);
