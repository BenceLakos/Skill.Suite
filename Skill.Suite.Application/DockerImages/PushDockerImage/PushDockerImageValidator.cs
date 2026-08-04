using FluentValidation;
using Skill.Suite.Application.DockerImages.Validation;

namespace Skill.Suite.Application.DockerImages.PushDockerImage;

public sealed class PushDockerImageValidator : AbstractValidator<PushDockerImageCommand>
{
    public PushDockerImageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Tag)
            .NotEmpty()
            .MaximumLength(128)
            .Must(t => DockerImageRules.TagPattern.IsMatch(t))
            .WithMessage("Tag must be alphanumeric plus '.', '_', '-' and cannot start with a dot or dash.");
    }
}
