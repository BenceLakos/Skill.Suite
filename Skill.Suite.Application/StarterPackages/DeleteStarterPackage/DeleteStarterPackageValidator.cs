using FluentValidation;

namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackage;

public sealed class DeleteStarterPackageValidator : AbstractValidator<DeleteStarterPackageCommand>
{
    public DeleteStarterPackageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(StarterPackageName.MaxLength)
            .Must(name => StarterPackageName.IsValid(name.Trim()))
            .WithMessage(StarterPackageErrors.InvalidName.Message);
    }
}
