using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.RequeueTestRun;

public sealed class RequeueTestRunHandler(
    IAppDbContext db,
    IBackgroundTaskQueue queue,
    IOptions<WebhookOptions> options,
    ILogger<RequeueTestRunHandler> logger)
    : IRequestHandler<RequeueTestRunCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(RequeueTestRunCommand request, CancellationToken cancellationToken)
    {
        var original = await db.TestRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.TestRunId, cancellationToken);

        if (original is null)
            return TestRunErrors.NotFound(request.TestRunId);

        // A NEW run, rather than resetting the old one. The original stays exactly as it was, because an expert
        // settling a dispute needs to see what happened the first time — including that it was re-judged and why
        // the two differ. Resetting would destroy the only record of the failure being recovered from.
        var competitorUsername = original.CompetitorId is null
            ? null
            : await db.Competitors
                .Where(c => c.Id == original.CompetitorId)
                .Select(c => c.Username)
                .FirstOrDefaultAsync(cancellationToken);

        var folderName = SubmissionFolderNameGenerator.Generate(
            options.Value.FolderTemplate, competitorUsername, original.CommitSha);

        var replay = TestRun.Create(
            original.SessionId,
            original.CompetitorId,
            original.RepositoryUrl,
            original.RepositoryName,
            original.Branch,
            original.CommitSha,
            folderName,
            original.JudgementImage);

        db.TestRuns.Add(replay);

        // CancellationToken.None from here, for the same reason the webhook uses it: a run persisted but never
        // enqueued is invisible work, and a request the operator abandoned mid-click must not create one.
        await db.SaveChangesAsync(CancellationToken.None);
        await queue.EnqueueAsync(new TestRunWorkItem(replay.Id), CancellationToken.None);

        logger.LogInformation(
            "Run {OriginalId} re-queued as {ReplayId} for commit {CommitSha}.",
            original.Id, replay.Id, original.CommitSha);

        return replay.Id;
    }
}
