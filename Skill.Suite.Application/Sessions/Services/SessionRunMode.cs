namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// Which of a session's two container sets is being planned: the competition's, or marking's.
/// </summary>
/// <remarks>
/// The two are the same images with the same placeholders, and differ in exactly two ways: the container
/// names carry a suffix and a label so the sets never collide or get torn down together, and every
/// database-scoped placeholder resolves to the SQL Server administrator rather than to the competitor. The
/// second is the point of marking — an expert opens a competitor's database to read what is in it, which the
/// competitor's own login may have no rights to do, while the database itself stays that competitor's.
/// </remarks>
public enum SessionRunMode
{
    /// <summary>The containers competitors work against, started by Start Session.</summary>
    Competition,

    /// <summary>The containers experts mark against, started once the session is closed.</summary>
    Marking,
}
