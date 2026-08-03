using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Credentials.Events;

public sealed record CredentialCreatedEvent(Guid CredentialId) : IDomainEvent;
