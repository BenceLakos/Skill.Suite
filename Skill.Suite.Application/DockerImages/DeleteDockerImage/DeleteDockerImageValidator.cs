using FluentValidation;

namespace Skill.Suite.Application.DockerImages.DeleteDockerImage;

public sealed class DeleteDockerImageValidator : AbstractValidator<DeleteDockerImageCommand>
{
    public DeleteDockerImageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
