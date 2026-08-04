using FluentValidation;
using Skill.Suite.Application.Sessions.Validation;

namespace Skill.Suite.Application.Sessions.CreateSession;

public sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(120)
            .Must(s => SessionRules.SlugPattern.IsMatch(s.ToLowerInvariant()))
            .WithMessage("Slug must be lowercase letters, numbers and dashes only.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
        RuleFor(x => x.TemplateFolder).MaximumLength(1000);
        RuleFor(x => x.JudgementImage).MaximumLength(500);
        RuleFor(x => x.DatabaseName).MaximumLength(120);
        RuleFor(x => x.DockerImages).ValidDockerImages();
    }
}
