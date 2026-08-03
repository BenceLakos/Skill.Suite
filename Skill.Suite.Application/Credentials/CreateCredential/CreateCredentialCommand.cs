using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.CreateCredential;

public sealed record CreateCredentialCommand(
    string Name,
    CredentialKind Kind,
    string Secret) : IRequest<Result<CredentialDto>>;
