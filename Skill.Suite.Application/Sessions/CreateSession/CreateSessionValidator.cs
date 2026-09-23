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

        // Length only, on purpose: an empty judgement image is a session that is marked by hand, and the
        // pull credential is deliberately not tied to it — the same credential pulls the session's own
        // service images, so refusing one without the other would reject a perfectly valid configuration.
        RuleFor(x => x.JudgementImage).MaximumLength(500);
        RuleFor(x => x.DatabaseName).ValidDatabaseBaseName();
        RuleFor(x => x.DatabaseSeedScript).ValidSeedScript(x => x.DatabaseName);
        RuleFor(x => x.DockerImages).ValidDockerImages(x => x.DatabaseName);
    }
}
