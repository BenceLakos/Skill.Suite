using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions.Events;

namespace Skill.Suite.Domain.Sessions;

public sealed class Session : AuditableEntity<Guid>
{
    private Session() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public SessionStatus Status { get; private set; }

    public string? TemplateFolder { get; private set; }
    public string? JudgementImage { get; private set; }

    public string? DatabaseName { get; private set; }
    public bool DatabaseReadAccess { get; private set; }
    public bool DatabaseWriteAccess { get; private set; }

    /// <summary>Credential used by the webhook to <c>git clone</c> competitor submissions.</summary>
    public Guid? GitCredentialId { get; private set; }

    /// <summary>Credential used by the runner to <c>docker pull</c> the judgement image.</summary>
    public Guid? JudgementImagePullCredentialId { get; private set; }

    public List<SessionDockerImage> DockerImages { get; private set; } = new();

    /// <summary>
    /// One repository per competitor, written by provisioning and read by the webhook to attribute a push.
    /// </summary>
    public List<SessionCompetitor> Competitors { get; private set; } = new();

    /// <summary>
    /// HMAC key shared with the git host's webhook, generated at <see cref="Start"/>.
    /// </summary>
    /// <remarks>
    /// Stored protected, like a credential secret: it is the only thing standing between the judgement
    /// pipeline and anyone who can reach the webhook endpoint. Regenerated on every Start, so a re-provision
    /// invalidates hooks left behind by an earlier attempt.
    /// </remarks>
    public byte[]? WebhookSecret { get; private set; }

    /// <summary>
    /// Organisation on the git host that owns this session's repositories.
    /// </summary>
    /// <remarks>
    /// Deliberately derived from <see cref="Slug"/> rather than stored: the slug is already unique, indexed
    /// and constrained to the characters an organisation name allows, so the owner segment of an incoming
    /// push resolves straight back to one session with no extra column and nothing that can drift out of
    /// step with the session's identity.
    /// </remarks>
    public string GitOrganization => Slug;

    /// <summary>
    /// Repository inside the organisation holding the starter package every competitor repository is copied
    /// from. Underscore-prefixed so it cannot collide with a competitor username.
    /// </summary>
    public const string TemplateRepositoryName = "_template";

    public static Result<Session> Create(
        string name,
        string slug,
        string? description,
        DateTime startsAt,
        DateTime endsAt,
        string? templateFolder,
        string? judgementImage,
        string? databaseName,
        bool databaseReadAccess,
        bool databaseWriteAccess,
        Guid? gitCredentialId,
        Guid? judgementImagePullCredentialId,
        IEnumerable<SessionDockerImage> dockerImages)
    {
        if (endsAt <= startsAt)
            return SessionErrors.InvalidDateRange;

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = NormalizeOptional(description),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = SessionStatus.Draft,
            TemplateFolder = NormalizeOptional(templateFolder),
            JudgementImage = NormalizeOptional(judgementImage),
            DatabaseName = NormalizeOptional(databaseName),
            DatabaseReadAccess = databaseReadAccess,
            DatabaseWriteAccess = databaseWriteAccess,
            GitCredentialId = gitCredentialId,
            JudgementImagePullCredentialId = judgementImagePullCredentialId,
            DockerImages = dockerImages.ToList(),
        };

        session.RaiseDomainEvent(new SessionCreatedEvent(session.Id));
        return session;
    }

    /// <summary>
    /// Edits the session's configuration. The status is deliberately not among it.
    /// </summary>
    /// <remarks>
    /// Every status change is a guarded transition that does external work — <see cref="Start"/> provisions,
    /// <see cref="Stop"/> withdraws access, <see cref="Close"/> ends the session. Letting an edit assign the
    /// status bypassed all three, producing a session that reads as live and judges nothing.
    /// </remarks>
    public Result UpdateDetails(
        string name,
        string? description,
        DateTime startsAt,
        DateTime endsAt,
        string? templateFolder,
        string? judgementImage,
        string? databaseName,
        bool databaseReadAccess,
        bool databaseWriteAccess,
        Guid? gitCredentialId,
        Guid? judgementImagePullCredentialId,
        IEnumerable<SessionDockerImage> dockerImages)
    {
        if (endsAt <= startsAt)
            return Result.Failure(SessionErrors.InvalidDateRange);

        Name = name.Trim();
        Description = NormalizeOptional(description);
        StartsAt = startsAt;
        EndsAt = endsAt;
        TemplateFolder = NormalizeOptional(templateFolder);
        JudgementImage = NormalizeOptional(judgementImage);
        DatabaseName = NormalizeOptional(databaseName);
        DatabaseReadAccess = databaseReadAccess;
        DatabaseWriteAccess = databaseWriteAccess;
        GitCredentialId = gitCredentialId;
        JudgementImagePullCredentialId = judgementImagePullCredentialId;
        DockerImages = dockerImages.ToList();

        RaiseDomainEvent(new SessionUpdatedEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Opens the session for submissions and stamps the webhook secret provisioning will install.
    /// </summary>
    /// <remarks>
    /// Deliberately does not perform the provisioning itself: creating an organisation, a template
    /// repository and N competitor repositories is a sequence of network calls that cannot run inside the
    /// request that flips the status. The caller enqueues that work; this method only guards the transition.
    /// </remarks>
    public Result Start(byte[] webhookSecret)
    {
        if (Status == SessionStatus.Closed)
            return Result.Failure(SessionErrors.AlreadyClosed);

        // Anything short of Closed may be started, deliberately. A stopped session is started again to resume
        // the competition, and an active one because provisioning talks to a git host over a network for
        // every competitor, so a run that half-succeeded has to be resumable. Refusing here would have left
        // the only repair being to close the session and rebuild it from scratch.
        // The secret is regenerated and the hooks reinstalled with it, so a delivery already in flight and
        // signed with the previous secret is rejected - which is why this is an explicit admin action.
        if (string.IsNullOrWhiteSpace(JudgementImage))
            return Result.Failure(SessionErrors.MissingJudgementImage);

        if (string.IsNullOrWhiteSpace(TemplateFolder))
            return Result.Failure(SessionErrors.MissingTemplateFolder);

        Status = SessionStatus.Active;
        WebhookSecret = webhookSecret;
        RaiseDomainEvent(new SessionStartedEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Suspends the session: it stops accepting pushes, without being closed for good.
    /// </summary>
    /// <remarks>
    /// Reversible on purpose, which is the whole difference from <see cref="Close"/>: <see cref="Start"/> is
    /// allowed again from here and restores everything stopping withdrew. Only an Active session can be
    /// stopped — a Draft one has nothing to withdraw, and a Closed one is already past this.
    /// <para>
    /// Like <see cref="Start"/>, this only guards the transition. Revoking the competitors' access to their
    /// repositories and stopping the service containers is the caller's work.
    /// </para>
    /// </remarks>
    public Result Stop()
    {
        if (Status != SessionStatus.Active)
            return Result.Failure(SessionErrors.NotActive);

        Status = SessionStatus.Stopped;
        RaiseDomainEvent(new SessionStoppedEvent(Id));
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == SessionStatus.Closed)
            return Result.Failure(SessionErrors.AlreadyClosed);

        Status = SessionStatus.Closed;
        RaiseDomainEvent(new SessionClosedEvent(Id));
        return Result.Success();
    }

    /// <summary>Records the outcome of provisioning one competitor's repository.</summary>
    public SessionCompetitor EnrolCompetitor(Guid competitorId, string repositoryName)
    {
        var existing = Competitors.FirstOrDefault(c => c.CompetitorId == competitorId);
        if (existing is not null)
        {
            // Re-running Start after a partial failure must retry the competitor, not duplicate the row -
            // a second row would make the webhook's repository lookup ambiguous.
            existing.MarkPending();
            return existing;
        }

        var enrolment = SessionCompetitor.Create(Id, competitorId, repositoryName);
        Competitors.Add(enrolment);
        return enrolment;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
