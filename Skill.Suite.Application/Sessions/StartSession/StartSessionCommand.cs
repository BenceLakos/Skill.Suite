using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.StartSession;

/// <summary>
/// Opens a session for submissions and provisions everything it runs on: a git organisation, a template
/// repository seeded from the session's template folder, one repository per competitor who has an account on
/// the git host, access to the session's shared database for every competitor who has a SQL login, the
/// session's docker services, and a push webhook.
/// </summary>
/// <remarks>
/// Safe to re-run. Every external step treats "already exists" as success, so starting a session whose
/// provisioning partly failed retries only what did not get through.
/// <para>
/// Competitors with no account on the git host are skipped rather than provisioned — they could not reach
/// the repository anyway — and reported in <see cref="StartSessionResult.SkippedNoGitAccess"/>; competitors
/// with no SQL login are skipped by the database stage and reported in
/// <see cref="StartSessionResult.SkippedNoDatabaseLogin"/>.
/// </para>
/// <para>
/// No stage aborts another. A database that cannot be created or a service that will not start is recorded
/// against the item it happened to and reported in <see cref="StartSessionResult.Failed"/>, while the rest of
/// the session is provisioned regardless — the alternative is a competition where nobody has a repository
/// because one container's port was taken.
/// </para>
/// <para>
/// <paramref name="Progress"/> is optional and receives a report as each stage advances, so a caller that
/// has somewhere to draw it can show how far provisioning has got instead of a spinner. Carrying a callback
/// on a request is only acceptable because this command never leaves the process: Mediator is
/// source-generated and in-process here, and the sink is invoked on the sending thread's terms.
/// </para>
/// </remarks>
public sealed record StartSessionCommand(
    Guid Id,
    IProgress<StartSessionProgress>? Progress = null) : IRequest<Result<StartSessionResult>>;
