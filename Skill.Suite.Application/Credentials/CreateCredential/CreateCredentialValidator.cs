using FluentValidation;
using Skill.Suite.Application.Credentials.Validation;

namespace Skill.Suite.Application.Credentials.CreateCredential;

public sealed class CreateCredentialValidator : AbstractValidator<CreateCredentialCommand>
{
    public CreateCredentialValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120)
            .Must(n => CredentialRules.NamePattern.IsMatch(n))
            .WithMessage("Name may contain only letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.Secret).NotEmpty().MaximumLength(4096);
    }
}
