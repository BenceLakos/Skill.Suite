using FluentValidation;

namespace Skill.Suite.Application.Experts.UpdateExpert;

public sealed class UpdateExpertValidator : AbstractValidator<UpdateExpertCommand>
{
    public UpdateExpertValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}
