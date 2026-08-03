using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.GetTestRun;

public sealed class GetTestRunHandler(IAppDbContext db) : IRequestHandler<GetTestRunQuery, Result<TestRunDto>>
{
    public async ValueTask<Result<TestRunDto>> Handle(GetTestRunQuery request, CancellationToken cancellationToken)
    {
        var run = await db.TestRuns
            .AsNoTracking()
            .Include(r => r.Fixtures.OrderBy(f => f.StartedAt).ThenBy(f => f.Name))
            .ThenInclude(f => f.UnitTests.OrderBy(t => t.StartedAt).ThenBy(t => t.Name))
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        // Ordered explicitly: without it the database decides, so the admin view could list fixtures in a
        // different order than the competitor view (which does order) for the same run - and in a different
        // order again on the next page load, which makes comparing two runs needlessly hard.
        return run is null
            ? TestRunErrors.NotFound(request.Id)
            : TestRunMapper.ToDto(run);
    }
}
