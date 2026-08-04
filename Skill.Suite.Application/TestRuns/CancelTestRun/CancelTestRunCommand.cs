using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.CancelTestRun;

/// <summary>
/// Stops a run an operator has judged stuck, and moves it to a terminal state.
/// </summary>
/// <remarks>
/// Before this existed the only way out of a hung run was a new push from the competitor or the 20-minute
/// backstop, so a run wedged at the start of a session held one of the worker's slots for the whole window.
/// </remarks>
public sealed record CancelTestRunCommand(Guid TestRunId) : IRequest<Result>;
