namespace Skill.Suite.Application.Competitors.ProvisionCompetitorAccounts;

using FluentValidation;

public sealed class ProvisionCompetitorAccountsValidator
    : AbstractValidator<ProvisionCompetitorAccountsCommand>
{
    public ProvisionCompetitorAccountsValidator()
    {
        RuleFor(x => x.CompetitorId).NotEmpty();
    }
}
