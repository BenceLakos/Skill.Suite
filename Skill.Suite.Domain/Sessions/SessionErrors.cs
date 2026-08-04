using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Sessions;

public static class SessionErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Session.NotFound", $"Session '{id}' was not found.");

    public static readonly Error SlugConflict =
        Error.Conflict("Session.SlugConflict", "A session with that slug already exists.");

    public static readonly Error InvalidDateRange =
        Error.Validation("Session.InvalidDateRange", "The end date must be after the start date.");

    public static readonly Error AlreadyClosed =
        Error.Conflict("Session.AlreadyClosed", "The session is already closed.");

    public static readonly Error MissingJudgementImage =
        Error.Validation("Session.MissingJudgementImage",
            "Select a judgement image before starting the session — without one no push can be marked.");

    public static readonly Error MissingTemplateFolder =
        Error.Validation("Session.MissingTemplateFolder",
            "Set the template folder before starting the session — it is the starter package copied " +
            "into every competitor repository.");

    public static readonly Error MissingGitCredential =
        Error.Validation("Session.MissingGitCredential",
            "Select a git access credential before starting the session — provisioning needs it to create " +
            "the organisation and repositories.");

    public static readonly Error TemplateFolderNotFound =
        Error.Validation("Session.TemplateFolderNotFound",
            "The template folder does not exist or is empty inside the application container. It must be a " +
            "path the container can read, so it usually has to be mounted in.");

    public static readonly Error AnotherSessionActive =
        Error.Conflict("Session.AnotherSessionActive",
            "Another session is already active. Close it first — an incoming push is matched to a session " +
            "by its organisation, and two active sessions make a competitor's repository ambiguous.");

    public static readonly Error GitHostNotConfigured =
        Error.Validation("Session.GitHostNotConfigured",
            "Webhook:GitInternalBaseUrl is not configured, so the application does not know where the git " +
            "host is. Provisioning cannot create repositories without it.");

    public static readonly Error NoCompetitors =
        Error.Validation("Session.NoCompetitors",
            "There are no competitors to provision repositories for.");
}
