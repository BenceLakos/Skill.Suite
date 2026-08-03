using FluentValidation;

namespace Skill.Suite.Application.Sessions.StartSession;

public sealed class StartSessionValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
