using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.ReprocessTestRunLog;

/// <summary>
/// Admin-only: re-reads the JSON-lines log file that the judgement container left on the
/// workdir volume and replaces every fixture / unit test the run currently has with the
/// freshly-parsed result. Useful after a parser fix or when new event types are added.
/// </summary>
public sealed record ReprocessTestRunLogCommand(Guid TestRunId) : IRequest<Result>;
