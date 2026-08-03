using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.ListCredentials;

public sealed record ListCredentialsQuery(CredentialKind? Kind, string? Search) : IRequest<Result<List<CredentialDto>>>;
