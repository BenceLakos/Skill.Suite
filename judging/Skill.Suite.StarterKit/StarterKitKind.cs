namespace Skill.Suite.StarterKit;

/// <summary>
/// Which half of a session the competitor is being handed.
/// </summary>
/// <remarks>
/// The two are inverses, and the rewrite rules invert with them: a white-box competitor implements the services,
/// so the implementation is stubbed; a black-box competitor writes the tests, so the reference suite is emptied.
/// Getting this backwards hands out the answer, which is why it is an explicit choice rather than something
/// inferred from a folder name.
/// </remarks>
public enum StarterKitKind
{
    /// <summary>Strip a <c>*.Services</c> reference implementation to a compiling skeleton.</summary>
    WhiteBox,

    /// <summary>Empty a <c>*.UnitTests</c> reference suite, keeping only its harness wiring.</summary>
    BlackBox,
}
