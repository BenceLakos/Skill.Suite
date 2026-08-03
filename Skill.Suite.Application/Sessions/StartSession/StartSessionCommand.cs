using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// Opens a session for submissions and provisions the git host: an organisation, a template repository
/// seeded from the session's template folder, one repository per competitor copied from it, and a push
/// webhook.
/// </summary>
/// <remarks>
/// Safe to re-run. Every git-host step treats "already exists" as success, so starting a session whose
/// provisioning partly failed retries only the competitors that did not get a repository.
/// </remarks>
public sealed record StartSessionCommand(Guid Id) : IRequest<Result<StartSessionResult>>;
