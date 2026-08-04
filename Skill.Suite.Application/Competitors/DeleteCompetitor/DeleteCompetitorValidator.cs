using FluentValidation;

namespace Skill.Suite.Application.Competitors.DeleteCompetitor;

public sealed class DeleteCompetitorValidator : AbstractValidator<DeleteCompetitorCommand>
{
    public DeleteCompetitorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
