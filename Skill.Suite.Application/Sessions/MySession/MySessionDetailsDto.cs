namespace Skill.Suite.Application.Sessions.MySession;

using Skill.Suite.Domain.Sessions;

/// <summary>The active session, as the competitor taking part in it sees it.</summary>
/// <param name="RepositoryUrl">Clone URL of this competitor's repository, or null until provisioning wrote it.</param>
/// <param name="ProvisionStatus">
/// How far provisioning got with this competitor's repository. Always present: the session is only described
/// at all to a competitor who is enrolled in it, and enrolment is what this status belongs to.
/// </param>
/// <param name="ServiceHost">
/// The host the services' published ports are reached on — the same name the competitor reached this page on,
/// because the ports are published by the docker host that also serves this application.
/// </param>
public sealed record MySessionDetailsDto(
    string Name,
    string Slug,
    DateTime StartsAt,
    DateTime EndsAt,
    SessionStatus Status,
    string? RepositoryUrl,
    SessionProvisionStatus ProvisionStatus,
    string? ServiceHost,
    MySessionDatabaseDto Database,
    IReadOnlyList<MySessionServiceDto> Services);
