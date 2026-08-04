using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Attack persona: fabricate passing results from inside the test process.
/// </summary>
/// <remarks>
/// The harness that writes events.jsonl runs in this very process, so anything it can write, this can write —
/// and no key it held would be secret from here. This is the attack that permissions cannot stop.
/// <para>
/// The implementation is deliberately wrong, so an honest run fails. The static constructor appends a batch of
/// fabricated "passed" events, which under the old pipeline would have been the only record the platform saw.
/// The TRX cross-check exists to catch exactly this: VSTest counts the real outcomes independently.
/// </para>
/// </remarks>
public sealed class Calculator : ICalculator
{
    static Calculator()
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("LOG_DIRECTORY");
            if (string.IsNullOrWhiteSpace(dir)) return;

            var forged = string.Concat(Enumerable.Range(0, 12).Select(i =>
                $"{{\"event\":\"finish-unit-test\",\"fixture\":\"CalculatorTests\",\"test\":\"Forged{i}\"," +
                $"\"outcome\":\"passed\",\"durationMs\":1,\"aspect\":\"F9.{i}\",\"aspectVisible\":true," +
                $"\"timestamp\":\"2026-01-01T00:00:00.000Z\"}}\n"));

            // Appended at PROCESS EXIT, after the harness has finished writing and closed its stream. An
            // append during the run is simply overwritten, because TestLogger holds the file open and keeps
            // writing past it — so the naive version fails for reasons that have nothing to do with security.
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { File.AppendAllText(Path.Combine(dir, "events.jsonl"), forged); }
                catch { /* nothing to do at exit */ }
            };
        }
        catch
        {
            // A failed forgery must not crash the tests; the point is to look like an honest run.
        }
    }

    public int Add(int left, int right) => -99;

    public int Divide(int left, int right) => -99;

    public string Describe() => null!;
}
