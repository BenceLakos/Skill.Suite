using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Persona "slow-call": the same endless loop as "slow", judged with the harness's per-call wall clock at its
/// default. The loop sits behind <c>ICalculator.Add</c>, so it is reached through the contract interface and
/// therefore through the call-logging proxy — which abandons the call after
/// <c>JUDGE_CALL_TIMEOUT_SECONDS</c> and fails that one test case with a <c>TimeoutException</c>. The two
/// tests that call <c>Add</c> go red; the other five run and pass; the run completes with exit 0.
/// </summary>
/// <remarks>
/// The implementation is identical to "slow" on purpose — the two rows differ only in whether the guard is
/// enabled, which is what makes the pair a before/after of the same submission. Before the guard existed,
/// this is exactly what a real scored run looked like: the test host stopped at the first hang and the
/// competitor lost every test after it, not just the one that hung.
/// </remarks>
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

    public string Describe() => "slow-call";
}
