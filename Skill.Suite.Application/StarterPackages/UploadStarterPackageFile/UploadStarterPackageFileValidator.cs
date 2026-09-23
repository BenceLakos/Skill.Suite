namespace Skill.Suite.Application.StarterPackages.UploadStarterPackageFile;

using FluentValidation;

public sealed class UploadStarterPackageFileValidator : AbstractValidator<UploadStarterPackageFileCommand>
{
    public UploadStarterPackageFileValidator()
    {
        RuleFor(x => x.FolderPath)
            .Must(StarterPackagePath.IsInPackage)
            .WithMessage(StarterPackageErrors.NotInAPackage.Message);

        RuleFor(x => x.RelativePath)
            .Must(StarterPackagePath.IsSafeRelativePath)
            .WithMessage(StarterPackageErrors.InvalidPath.Message);

        RuleFor(x => x.Content).NotNull();
    }
}
