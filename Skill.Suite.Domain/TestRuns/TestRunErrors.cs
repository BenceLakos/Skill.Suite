using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns;

public static class TestRunErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("TestRun.NotFound", $"Test run '{id}' was not found.");

    public static readonly Error NoActiveSession =
        Error.Conflict("TestRun.NoActiveSession", "There is no active session to dispatch this submission to.");

    public static readonly Error SessionMissingJudgementImage =
        Error.Validation("TestRun.SessionMissingJudgementImage", "The active session does not declare a judgement image.");

    public static readonly Error CompetitorNotFound =
        Error.NotFound("TestRun.CompetitorNotFound",
            "No competitor is enrolled in this session for that repository. Start the session again to " +
            "provision the repositories, or check that the repository name matches the competitor's username.");

    public static Error UnknownOrganization(string? owner) =>
        Error.NotFound("TestRun.UnknownOrganization",
            string.IsNullOrWhiteSpace(owner)
                ? "The webhook payload did not identify a repository owner."
                : $"No active session owns the organisation '{owner}'.");

    public static readonly Error InvalidSignature =
        Error.Validation("TestRun.InvalidSignature", "The webhook signature is missing or invalid.");

    public static readonly Error TemplateRepositoryPush =
        Error.Validation("TestRun.TemplateRepositoryPush",
            "Pushes to the session's template repository are not submissions and are not judged.");

    public static readonly Error NotABranchPush =
        Error.Validation("TestRun.NotABranchPush", "Only pushes to a branch are judged.");

    public static readonly Error MissingRepositoryUrl =
        Error.Validation("TestRun.MissingRepositoryUrl", "The webhook payload must contain a repository URL.");

    public static readonly Error LogFileMissing =
        Error.NotFound("TestRun.LogFileMissing", "No log file was produced for this run.");
}
