using FluentValidation;

namespace Skill.Suite.Application.Webhooks.ProcessGitWebhook;

public sealed class ProcessGitWebhookValidator : AbstractValidator<ProcessGitWebhookCommand>
{
    public ProcessGitWebhookValidator()
    {
        RuleFor(x => x.RepositoryUrl).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RepositoryName).MaximumLength(200);
        RuleFor(x => x.Branch).MaximumLength(200);
        RuleFor(x => x.CommitSha).MaximumLength(64);
    }
}
