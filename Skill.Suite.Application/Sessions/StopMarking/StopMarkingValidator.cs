using FluentValidation;

namespace Skill.Suite.Application.Sessions.StopMarking;

public sealed class StopMarkingValidator : AbstractValidator<StopMarkingCommand>
{
    public StopMarkingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
