using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Credentials.Events;

public sealed record CredentialRemovedEvent(Guid CredentialId) : IDomainEvent;
