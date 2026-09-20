using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.UpdateSession;

public sealed record UpdateSessionCommand(
    Guid Id,
    string Name,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    string? TemplateFolder,
    string? JudgementImage,
    string? DatabaseName,
    bool DatabaseReadAccess,
    bool DatabaseWriteAccess,
    Guid? GitCredentialId,
    Guid? JudgementImagePullCredentialId,
    List<SessionDockerImage> DockerImages) : IRequest<Result>;
