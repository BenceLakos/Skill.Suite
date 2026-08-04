using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.RequeueTestRun;

/// <summary>
/// Judges a competitor's submission again, as a new run.
/// </summary>
/// <remarks>
/// This is the recovery path for a lost webhook delivery. Gitea does not retry a failed delivery, so before this
/// existed a submission whose one delivery timed out was simply never judged — and once the session closed there
/// was no way to notice, let alone fix it.
/// </remarks>
public sealed record RequeueTestRunCommand(Guid TestRunId) : IRequest<Result<Guid>>;
