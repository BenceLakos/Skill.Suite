using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.ReprocessTestRunLog;

public sealed class ReprocessTestRunLogHandler(
    IAppDbContext db,
    IOptions<WebhookOptions> options,
    ILogger<ReprocessTestRunLogHandler> logger)
    : IRequestHandler<ReprocessTestRunLogCommand, Result>
{
    public async ValueTask<Result> Handle(ReprocessTestRunLogCommand request, CancellationToken cancellationToken)
    {
        // Deliberately don't Include fixtures: we wipe them via ExecuteDelete below and
        // then let the parser populate a fresh collection. Tracking the old graph here
        // would race against the bulk delete and confuse SaveChanges.
        var run = await db.TestRuns
            .FirstOrDefaultAsync(r => r.Id == request.TestRunId, cancellationToken);

        if (run is null)
            return Result.Failure(TestRunErrors.NotFound(request.TestRunId));

        var opts = options.Value;
        var logFilePath = Path.Combine(opts.WorkingDirectory, run.FolderName + ".log", opts.EventLogFileName);

        if (!File.Exists(logFilePath))
            return Result.Failure(TestRunErrors.LogFileMissing);

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(logFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read log file {Path} during reprocess of {TestRunId}", logFilePath, run.Id);
            return Result.Failure(Error.Failure("TestRun.LogReadFailed", ex.Message));
        }

        // Wipe every fixture for this run. The fixture → unit-test relationship has
        // OnDelete(Cascade), so the units go with them in a single round-trip. This
        // happens before parsing so the new fixtures/units we materialise don't collide
        // with the old ones still living in the DB.
        await db.TestFixtureResults
            .Where(f => f.TestRunId == run.Id)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var line in lines)
            TestLogParser.Apply(run, line);

        // Same Add-explicitly trick as ExecuteTestRunHandler: with application-generated
        // Guid keys, EF's graph-attachment heuristic mistakes new entities for existing
        // ones and emits UPDATEs that affect 0 rows. db.Add() forces Added state.
        foreach (var fixture in run.Fixtures)
        {
            db.Add(fixture);
            foreach (var unit in fixture.UnitTests)
                db.Add(unit);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reprocessed {LineCount} log lines into {FixtureCount} fixtures for run {TestRunId}.",
            lines.Length, run.Fixtures.Count, run.Id);

        return Result.Success();
    }
}
