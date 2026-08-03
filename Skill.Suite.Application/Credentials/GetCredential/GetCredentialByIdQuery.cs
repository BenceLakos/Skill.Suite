using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Credentials.GetCredential;

public sealed record GetCredentialByIdQuery(Guid Id) : IRequest<Result<CredentialDto>>;
