using FluentValidation;

namespace Skill.Suite.Application.Sessions.StartMarking;

public sealed class StartMarkingValidator : AbstractValidator<StartMarkingCommand>
{
    public StartMarkingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
