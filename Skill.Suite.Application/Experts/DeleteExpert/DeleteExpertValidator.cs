using FluentValidation;

namespace Skill.Suite.Application.Experts.DeleteExpert;

public sealed class DeleteExpertValidator : AbstractValidator<DeleteExpertCommand>
{
    public DeleteExpertValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
