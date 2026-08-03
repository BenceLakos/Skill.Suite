using FluentValidation;
using Skill.Suite.Application.DockerImages.Validation;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.CreateDockerImage;

public sealed class CreateDockerImageValidator : AbstractValidator<CreateDockerImageCommand>
{
    public CreateDockerImageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120)
            .Must(n => DockerImageRules.NamePattern.IsMatch(n))
            .WithMessage("Name may contain only letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.ImageName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Source).IsInEnum();

        // Build-source images need a build context; pulled images do not.
        When(x => x.Source == DockerImageSource.Build, () =>
        {
            RuleFor(x => x.BuildContext)
                .NotEmpty()
                .MaximumLength(1000);

            RuleFor(x => x.DockerfilePath).MaximumLength(1000);

            RuleForEach(x => x.BuildArgs!)
                .Must(kv => DockerImageRules.BuildArgKeyPattern.IsMatch(kv.Key))
                .WithMessage("Build arg keys must match POSIX naming (letters, digits, underscore; cannot start with a digit).")
                .When(x => x.BuildArgs is not null);
        });
    }
}
