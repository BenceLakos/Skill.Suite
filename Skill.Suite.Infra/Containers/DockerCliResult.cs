namespace Skill.Suite.Infra.Containers;

/// <summary>Everything one finished <c>docker</c> invocation said.</summary>
internal sealed record DockerCliResult(int ExitCode, string StandardOutput, string StandardError);
