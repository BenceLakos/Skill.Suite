using Riok.Mapperly.Abstractions;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions;

[Mapper]
public static partial class SessionMapper
{
    // WebhookSecret is ignored deliberately, not incidentally: it is the HMAC key the git host signs
    // deliveries with, and a DTO reaches the Blazor circuit. Mapperly erroring on an unmapped source member
    // is what forces that to be a decision rather than an oversight — leave the ignore in place.
    [MapperIgnoreSource(nameof(Session.DomainEvents))]
    [MapperIgnoreSource(nameof(Session.WebhookSecret))]
    [MapperIgnoreSource(nameof(Session.Competitors))]
    public static partial SessionDto ToDto(Session session);

    public static partial List<SessionDto> ToDtoList(IEnumerable<Session> sessions);
}
