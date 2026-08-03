using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Authorization;
using Skill.Suite.Application.Webhooks;

namespace Skill.Suite.Endpoints;

public static class TestRunEndpoints
{
    public static IEndpointRouteBuilder MapTestRunEndpoints(this IEndpointRouteBuilder app)
    {
        // Log downloads are intentionally staff-only — competitors must not see the raw
        // event stream of their own runs. Anyone in the Competitor role gets 403 even if
        // they craft the URL by hand.
        var group = app.MapGroup("/api/test-runs")
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Admin},{Roles.Expert}" });

        group.MapGet("/{id:guid}/log", async (
            Guid id,
            IAppDbContext db,
            IOptions<WebhookOptions> webhookOptions,
            CancellationToken ct) =>
        {
            var run = await db.TestRuns
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new { r.Id, r.FolderName })
                .FirstOrDefaultAsync(ct);

            if (run is null)
                return Results.NotFound();

            // Defence-in-depth: even though SubmissionFolderNameGenerator sanitises the
            // folder name on creation, refuse to let any path-traversal sequence reach
            // Path.Combine here.
            if (run.FolderName.Contains("..") || run.FolderName.Contains('/') || run.FolderName.Contains('\\'))
                return Results.BadRequest(new { error = "Run folder name is invalid." });

            var opts = webhookOptions.Value;
            var path = Path.Combine(opts.WorkingDirectory, run.FolderName + ".log", opts.EventLogFileName);

            if (!File.Exists(path))
                return Results.NotFound(new { error = "Log file was not produced for this run." });

            var stream = File.OpenRead(path);
            var fileName = $"testrun-{run.Id:N}-{opts.EventLogFileName}";
            // application/x-ndjson is the de-facto type for JSON-lines streams.
            return Results.File(stream, "application/x-ndjson", fileName);
        });

        return app;
    }
}
