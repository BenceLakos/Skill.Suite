namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Everything the status view needs from the SQL Server instance, read in one round trip.
/// </summary>
/// <remarks>
/// One query for all competitors rather than one probe each: the status column refreshes on every page load,
/// and N round trips over a venue network is what makes a grid feel broken.
/// </remarks>
public sealed record MsSqlAccountInventory(
    IReadOnlyCollection<string> Logins,
    IReadOnlyCollection<string> Databases);
