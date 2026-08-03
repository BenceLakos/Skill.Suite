using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Application.Experts.CreateExpert;

public sealed class CreateExpertHandler(IAppDbContext db) : IRequestHandler<CreateExpertCommand, Result<ExpertDto>>
{
    public async ValueTask<Result<ExpertDto>> Handle(CreateExpertCommand request, CancellationToken cancellationToken)
    {
        var expert = Expert.Create(request.DisplayName);
        db.Experts.Add(expert);
        await db.SaveChangesAsync(cancellationToken);

        return ExpertMapper.ToDto(expert);
    }
}
