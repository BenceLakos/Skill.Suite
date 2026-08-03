using FluentValidation;
using Skill.Suite.Application.Sessions.Validation;

namespace Skill.Suite.Application.Sessions.UpdateSession;

public sealed class UpdateSessionValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.TemplateFolder).MaximumLength(1000);
        RuleFor(x => x.JudgementImage).MaximumLength(500);
        RuleFor(x => x.DatabaseName).MaximumLength(120);
        RuleFor(x => x.DockerImages).ValidDockerImages();
    }
}
