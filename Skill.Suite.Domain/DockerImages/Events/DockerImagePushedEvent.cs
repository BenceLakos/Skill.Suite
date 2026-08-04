using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.DockerImages.Events;

public sealed record DockerImagePushedEvent(Guid DockerImageId, string Tag) : IDomainEvent;
