using FluentValidation;

namespace Skill.Suite.Application.StarterPackages.UploadStarterPackage;

public sealed class UploadStarterPackageValidator : AbstractValidator<UploadStarterPackageCommand>
{
    public UploadStarterPackageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(StarterPackageName.MaxLength)
            .Must(name => StarterPackageName.IsValid(name.Trim()))
            .WithMessage(StarterPackageErrors.InvalidName.Message);

        RuleFor(x => x.Archive).NotNull();
    }
}
