using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.CreateSession;

public sealed record CreateSessionCommand(
    string Name,
    string Slug,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    string? TemplateFolder,
    string? JudgementImage,
    string? DatabaseName,
    bool DatabaseReadAccess,
    bool DatabaseWriteAccess,
    string? DatabaseSeedScript,
    Guid? GitCredentialId,
    Guid? JudgementImagePullCredentialId,
    List<SessionDockerImage> DockerImages) : IRequest<Result<SessionDto>>;
