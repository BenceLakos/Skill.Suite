using Riok.Mapperly.Abstractions;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Application.Experts;

[Mapper]
public static partial class ExpertMapper
{
    [MapperIgnoreSource(nameof(Expert.DomainEvents))]
    public static partial ExpertDto ToDto(Expert expert);

    public static partial List<ExpertDto> ToDtoList(IEnumerable<Expert> experts);
}
