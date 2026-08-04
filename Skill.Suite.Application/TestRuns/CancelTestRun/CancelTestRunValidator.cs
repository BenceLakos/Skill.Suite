using FluentValidation;

namespace Skill.Suite.Application.TestRuns.CancelTestRun;

public sealed class CancelTestRunValidator : AbstractValidator<CancelTestRunCommand>
{
    public CancelTestRunValidator()
    {
        RuleFor(x => x.TestRunId).NotEmpty();
    }
}
