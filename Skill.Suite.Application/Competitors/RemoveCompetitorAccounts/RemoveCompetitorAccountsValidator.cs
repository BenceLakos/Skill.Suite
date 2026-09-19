namespace Skill.Suite.Application.Competitors.RemoveCompetitorAccounts;

using FluentValidation;

public sealed class RemoveCompetitorAccountsValidator
    : AbstractValidator<RemoveCompetitorAccountsCommand>
{
    public RemoveCompetitorAccountsValidator()
    {
        RuleFor(x => x.CompetitorId).NotEmpty();
    }
}
