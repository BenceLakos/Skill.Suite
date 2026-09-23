namespace Skill.Suite.Services.FileDrop;

/// <summary>One file of a batch as <c>js/file-drop.js</c> lists it, before it is numbered.</summary>
internal sealed record FileDropManifestEntry(string Path, long Size);
