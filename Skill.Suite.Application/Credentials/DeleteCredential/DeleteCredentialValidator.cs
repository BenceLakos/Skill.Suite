using FluentValidation;

namespace Skill.Suite.Application.Credentials.DeleteCredential;

public sealed class DeleteCredentialValidator : AbstractValidator<DeleteCredentialCommand>
{
    public DeleteCredentialValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
