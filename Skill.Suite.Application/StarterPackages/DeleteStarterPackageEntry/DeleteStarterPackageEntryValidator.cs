namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackageEntry;

using FluentValidation;

public sealed class DeleteStarterPackageEntryValidator : AbstractValidator<DeleteStarterPackageEntryCommand>
{
    public DeleteStarterPackageEntryValidator()
    {
        // Stopped at the first failure, so a path outside any package is not also told it is a whole package.
        RuleFor(x => x.RelativePath)
            .Cascade(CascadeMode.Stop)
            .Must(StarterPackagePath.IsInPackage)
            .WithMessage(StarterPackageErrors.NotInAPackage.Message)
            .Must(StarterPackagePath.IsBelowPackage)
            .WithMessage((_, path) => StarterPackageErrors.EntryIsAPackage(path).Message);
    }
}
