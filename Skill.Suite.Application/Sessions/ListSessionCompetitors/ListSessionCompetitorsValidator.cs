using FluentValidation;

namespace Skill.Suite.Application.Sessions.ListSessionCompetitors;

public sealed class ListSessionCompetitorsValidator : AbstractValidator<ListSessionCompetitorsQuery>
{
    public ListSessionCompetitorsValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
