using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.DockerImages.Events;

public sealed record DockerImageCreatedEvent(Guid DockerImageId) : IDomainEvent;
