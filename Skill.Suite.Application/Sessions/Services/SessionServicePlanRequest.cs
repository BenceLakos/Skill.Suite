namespace Skill.Suite.Application.Sessions.Services;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Everything <see cref="SessionServicePlanner"/> needs to decide which containers a session runs.
/// </summary>
/// <param name="Stage">
/// The stage a per-competitor failure is recorded under, so the same planner can serve the competition's
/// docker stage and marking's without either inventing the other's label.
/// </param>
/// <param name="DatabaseBaseName">
/// The session's database base name, or null when it configures no database — in which case no competitor
/// has one and a database-scoped service is started for nobody.
/// </param>
/// <param name="DatabaseServer">The SQL Server as a service container reaches it.</param>
/// <param name="DatabaseAdmin">
/// The SQL Server administrator, used to resolve database-scoped placeholders in
/// <see cref="SessionRunMode.Marking"/> and ignored in <see cref="SessionRunMode.Competition"/>, where every
/// competitor connects as themselves.
/// </param>
/// <param name="GitInternalBaseUrl">
/// Where the platform's own registry is, so an image reference stored in this process's terms is restated in
/// the host daemon's before the container is asked for.
/// </param>
internal sealed record SessionServicePlanRequest(
    Session Session,
    SessionRunMode Mode,
    SessionProvisioningStage Stage,
    IReadOnlyList<SessionServicePlanCompetitor> Competitors,
    string? DatabaseBaseName,
    string? DatabaseServer,
    BasicCredential? DatabaseAdmin,
    string? GitInternalBaseUrl);
