using FluentValidation;

namespace Skill.Suite.Application.Sessions.StopSession;

public sealed class StopSessionValidator : AbstractValidator<StopSessionCommand>
{
    public StopSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
