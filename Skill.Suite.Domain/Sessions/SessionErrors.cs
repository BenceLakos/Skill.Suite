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

    public static readonly Error NotActive =
        Error.Validation("Session.NotActive", "Only an active session can be stopped.");

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

    public static readonly Error GitAccessUnknown =
        Error.Failure("Session.GitAccessUnknown",
            "The git server's user list could not be read, so there is no way to tell which competitors can " +
            "reach a repository. The session was not started and nothing was created — check the git server " +
            "and the git access credential, then start it again.");

    public static readonly Error MissingDatabaseCredential =
        Error.Validation("Session.MissingDatabaseCredential",
            "This session has a database configured but no Microsoft SQL Server credential exists. Add one " +
            "on the Credentials page, or clear the session's database name.");

    public static readonly Error DatabaseAccessUnknown =
        Error.Failure("Session.DatabaseAccessUnknown",
            "The SQL Server's login list could not be read, so there is no way to tell which competitors can " +
            "be given a session database. The session was not started and nothing was created — check the " +
            "SQL Server and its credential, then start it again.");

    public static Error SeedScriptUnreadable(string path, string reason) =>
        Error.Validation("Session.SeedScriptUnreadable",
            $"The database seed script '{path}' could not be read from the starter packages volume: {reason} " +
            "The session was not started — pick a script that is still there, or clear the field.");

    public static readonly Error SeedScriptWithoutDatabase =
        Error.Validation("Session.SeedScriptWithoutDatabase",
            "This session has a database seed script but no database name, so there is nothing to run it " +
            "against. Set the database name, or clear the seed script.");

    public static readonly Error MissingImagePullCredential =
        Error.Validation("Session.MissingImagePullCredential",
            "The image pull credential this session names no longer exists, so its docker services cannot be " +
            "pulled. Select a credential on the session, or remove the one it points at.");

    public static Error ServicePortOutOfRange(int basePort, int ordinal) =>
        Error.Validation("Session.ServicePortOutOfRange",
            $"Host port {basePort} plus this competitor's position in the session ({ordinal}) is past 65535, " +
            "so no port could be published for them. Lower the service's host port — it is a base that every " +
            "competitor's own copy is counted up from.");

    public static readonly Error NotClosedForMarking =
        Error.Conflict("Session.NotClosedForMarking",
            "Marking can only be started for a closed session. Close it first — a session that is still " +
            "running, or only stopped, can be started again, and its competition containers hold the very " +
            "host ports the marking containers publish.");

    public static readonly Error NoServicesToMark =
        Error.Validation("Session.NoServicesToMark",
            "This session configures no docker services, so there is nothing to start for marking.");

    public static readonly Error MarkingNeedsDatabaseName =
        Error.Validation("Session.MarkingNeedsDatabaseName",
            "A docker service of this session refers to the competitors' database, but the session has no " +
            "database base name, so there is no database to point the marking containers at.");

    public static readonly Error NoCompetitorsWithGitAccess =
        Error.Validation("Session.NoCompetitorsWithGitAccess",
            "No competitor has an account on the git server, so every repository created would be " +
            "unreachable. Provision their git accounts on the Competitors page first, then start the " +
            "session.");
}
