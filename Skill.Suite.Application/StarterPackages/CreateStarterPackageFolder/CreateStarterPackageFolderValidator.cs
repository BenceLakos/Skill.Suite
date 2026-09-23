namespace Skill.Suite.Application.StarterPackages.CreateStarterPackageFolder;

using FluentValidation;

public sealed class CreateStarterPackageFolderValidator : AbstractValidator<CreateStarterPackageFolderCommand>
{
    public CreateStarterPackageFolderValidator()
    {
        RuleFor(x => x.ParentPath)
            .Must(StarterPackagePath.IsInPackage)
            .WithMessage(StarterPackageErrors.NotInAPackage.Message);

        // Judged as the handler will use it: a name typed with a space either side means the name without.
        RuleFor(x => x.Name)
            .Must(name => StarterPackageEntryName.IsValid(name?.Trim()))
            .WithMessage(StarterPackageErrors.InvalidEntryName.Message);
    }
}
