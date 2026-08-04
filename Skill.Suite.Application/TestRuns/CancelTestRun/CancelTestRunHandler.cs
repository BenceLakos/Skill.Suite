using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.CancelTestRun;

public sealed class CancelTestRunHandler(
    IAppDbContext db,
    IActiveTestRunRegistry registry,
    ILogger<CancelTestRunHandler> logger)
    : IRequestHandler<CancelTestRunCommand, Result>
{
    public async ValueTask<Result> Handle(CancelTestRunCommand request, CancellationToken cancellationToken)
    {
        // Filtered UPDATE rather than load-mutate-save, and the filter is what makes it safe to press twice: a
        // run that reached a terminal state between the page rendering and the click is left alone rather than
        // having a finished result overwritten with Cancelled.
        var affected = await db.TestRuns
            .Where(r => r.Id == request.TestRunId &&
                        (r.Status == TestRunStatus.Pending ||
                         r.Status == TestRunStatus.Cloning ||
                         r.Status == TestRunStatus.Running))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, TestRunStatus.Cancelled)
                .SetProperty(r => r.FailureReason, TestRunReasons.CancelledByOperator)
                .SetProperty(r => r.FinishedAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);

        if (affected == 0)
        {
            var exists = await db.TestRuns.AnyAsync(r => r.Id == request.TestRunId, cancellationToken);
            return exists
                ? Result.Failure(TestRunErrors.AlreadyTerminal)
                : Result.Failure(TestRunErrors.NotFound(request.TestRunId));
        }

        // Signalled after the row is terminal, so the executing handler cannot race back and overwrite it. The
        // registry only knows about runs this process is executing; a run abandoned by a previous process has
        // nothing to signal, which is exactly why the database write comes first.
        var signalled = registry.CancelForRun(request.TestRunId);

        logger.LogInformation(
            "Run {TestRunId} cancelled by an operator (container signalled: {Signalled}).",
            request.TestRunId, signalled);

        return Result.Success();
    }
}
