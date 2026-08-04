using FluentValidation;
using Skill.Suite.Application.Credentials.Validation;

namespace Skill.Suite.Application.Credentials.UpdateCredential;

public sealed class UpdateCredentialValidator : AbstractValidator<UpdateCredentialCommand>
{
    public UpdateCredentialValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120)
            .Must(n => CredentialRules.NamePattern.IsMatch(n))
            .WithMessage("Name may contain only letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.Secret).NotEmpty().MaximumLength(4096);
    }
}
