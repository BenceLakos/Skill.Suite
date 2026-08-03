using FluentValidation;

namespace Skill.Suite.Application.Sessions.CloseSession;

public sealed class CloseSessionValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
