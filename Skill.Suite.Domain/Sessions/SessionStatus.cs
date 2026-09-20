namespace Skill.Suite.Domain.Sessions;

public enum SessionStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2,

    /// <summary>
    /// Suspended: no longer accepting pushes, but startable again.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Closed"/> this is reversible, which is what makes it useful for a break in the
    /// competition and for repairing a half-finished start — everything stopping withdrew is restored by
    /// starting the session again.
    /// </remarks>
    Stopped = 3,
}
