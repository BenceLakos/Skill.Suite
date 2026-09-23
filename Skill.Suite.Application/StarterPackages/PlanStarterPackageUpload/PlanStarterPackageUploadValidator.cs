namespace Skill.Suite.Application.StarterPackages.PlanStarterPackageUpload;

using FluentValidation;

public sealed class PlanStarterPackageUploadValidator : AbstractValidator<PlanStarterPackageUploadQuery>
{
    public PlanStarterPackageUploadValidator()
    {
        RuleFor(x => x.FolderPath)
            .Must(StarterPackagePath.IsInPackage)
            .WithMessage(StarterPackageErrors.NotInAPackage.Message);

        RuleFor(x => x.Files).NotEmpty();

        RuleForEach(x => x.Files)
            .NotNull()
            .ChildRules(file =>
            {
                file.RuleFor(f => f.RelativePath)
                    .Must(StarterPackagePath.IsSafeRelativePath)
                    .WithMessage(StarterPackageErrors.InvalidPath.Message);

                file.RuleFor(f => f.SizeBytes).GreaterThanOrEqualTo(0);
            });
    }
}
