namespace Skill.Suite.Components.Pages.Sessions;

/// <summary>What the marking dialog was closed with: which competitor, from which machine, and to do what.</summary>
public sealed record MarkingRequest(MarkingAction Action, Guid CompetitorId, string MarkingIpAddress);
