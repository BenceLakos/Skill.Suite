namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>One competitor whose repository could not be provisioned, and why.</summary>
public sealed record StartSessionFailure(string Username, string Error);
