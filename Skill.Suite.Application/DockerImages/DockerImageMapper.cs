using Riok.Mapperly.Abstractions;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages;

[Mapper]
public static partial class DockerImageMapper
{
    [MapperIgnoreSource(nameof(DockerImage.DomainEvents))]
    public static partial DockerImageDto ToDto(DockerImage image);

    public static partial List<DockerImageDto> ToDtoList(IEnumerable<DockerImage> images);
}
