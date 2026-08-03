using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Credentials.DeleteCredential;

public sealed record DeleteCredentialCommand(Guid Id) : IRequest<Result>;
