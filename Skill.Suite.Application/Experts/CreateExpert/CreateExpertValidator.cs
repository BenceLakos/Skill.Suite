using FluentValidation;

namespace Skill.Suite.Application.Experts.CreateExpert;

public sealed class CreateExpertValidator : AbstractValidator<CreateExpertCommand>
{
    public CreateExpertValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}
