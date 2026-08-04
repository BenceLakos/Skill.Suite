using FluentValidation;

namespace Skill.Suite.Application.TestRuns.RequeueTestRun;

public sealed class RequeueTestRunValidator : AbstractValidator<RequeueTestRunCommand>
{
    public RequeueTestRunValidator()
    {
        RuleFor(x => x.TestRunId).NotEmpty();
    }
}
