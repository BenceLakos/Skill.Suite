using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.DockerImages.Events;

public sealed record DockerImageRemovedEvent(Guid DockerImageId) : IDomainEvent;
