using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Persona "slow-hardened": the same endless loop as "slow", judged under the capability set the platform
/// actually applies — <c>--cap-drop ALL</c> with SETUID, SETGID, CHOWN, DAC_OVERRIDE, FOWNER and KILL added
/// back, plus <c>no-new-privileges</c>. The submission is identical to "slow" on purpose; the variable is
/// the container, not the code.
/// <para>
/// It was added when CAP_KILL was missing from that set: the test step runs as an unprivileged account, so
/// root's own <c>timeout</c> could not signal it and reported 137 instead of 124, which the judge read as a
/// successful run. A matrix run keeps the default capabilities, so "slow" could never reproduce it. The
/// capability is granted now and this run takes the same 124 path as "slow" — what the row still proves is
/// that the platform's real flag set runs the pipeline end to end, <c>setpriv</c> included, and that the
/// wall clock fires under it. The 137 classification is covered directly by judge-selftest.sh.
/// </para>
/// </summary>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right)
    {
        // Never returns.
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "slow-hardened";
}
