namespace Skill.Suite.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn => DateTime.UtcNow;
}
