namespace Skill.Suite.StarterKit;

/// <summary>What the generator produced, for the CLI to report.</summary>
public sealed record StarterKitResult(string ProjectFile, IReadOnlyList<string> StubbedFiles);
