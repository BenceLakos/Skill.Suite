namespace Skill.Suite.Marker.Model;

/// <summary>
/// Mutation-testing outcomes for one part.
/// </summary>
/// <param name="Killed">Mutants the suite detected.</param>
/// <param name="Survived">Mutants the suite failed to detect.</param>
/// <param name="Timeout">Mutants that hung the suite — counted as detected.</param>
/// <param name="NoCoverage">Mutants no test exercised.</param>
/// <param name="Other">Everything else: compile errors, ignored, unknown status.</param>
public readonly record struct MutationStats(int Killed, int Survived, int Timeout, int NoCoverage, int Other)
{
    /// <summary>
    /// Mutants the suite actually ran against. <see cref="NoCoverage"/> and <see cref="Other"/> are
    /// excluded so unreachable code cannot dilute the kill rate in either direction.
    /// </summary>
    public int Covered => Killed + Survived + Timeout;

    /// <summary>
    /// Every mutant generated, whatever became of it.
    /// </summary>
    /// <remarks>
    /// Reported alongside <see cref="Covered"/> so a reader can see how much of the mutant population the
    /// suite never reached. Previously only the covered count was on the wire, which made
    /// <see cref="NoCoverage"/>'s share of the whole impossible to compute from the event.
    /// </remarks>
    public int Total => Killed + Survived + Timeout + NoCoverage + Other;

    /// <summary>
    /// Fraction of covered mutants detected; 0 when none were covered. A timeout counts as a kill — the
    /// suite did notice the change, just by hanging on it.
    /// </summary>
    public double KillRate => Covered == 0 ? 0.0 : (double)(Killed + Timeout) / Covered;

    /// <summary>Adds another part's outcomes to this one.</summary>
    public MutationStats Add(MutationStats other) =>
        new(Killed + other.Killed, Survived + other.Survived, Timeout + other.Timeout,
            NoCoverage + other.NoCoverage, Other + other.Other);
}
