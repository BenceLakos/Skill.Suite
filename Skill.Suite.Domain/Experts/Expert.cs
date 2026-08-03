using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Experts.Events;

namespace Skill.Suite.Domain.Experts;

public sealed class Expert : AuditableEntity<Guid>
{
    private Expert() { }

    public string DisplayName { get; private set; } = string.Empty;

    public static Expert Create(string displayName)
    {
        var expert = new Expert
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
        };

        expert.RaiseDomainEvent(new ExpertCreatedEvent(expert.Id));
        return expert;
    }

    public void Rename(string displayName)
    {
        DisplayName = displayName.Trim();
        RaiseDomainEvent(new ExpertUpdatedEvent(Id));
    }

    public void MarkRemoved() =>
        RaiseDomainEvent(new ExpertRemovedEvent(Id));
}
