namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions;
using Xunit;

/// <summary>
/// The name one competitor's session database gets, and the limit it has to stay inside.
/// </summary>
/// <remarks>
/// Worth asserting on its own because two separate parts of the system compute it and have to agree: Start
/// Session creates the database under this name, and the competitor's own page prints it for them to paste
/// into a connection string. A disagreement would send every competitor to a database that does not exist.
/// <para>
/// The separation is the point of the naming. Two competitors must never derive one name, and a name that
/// silently overflowed SQL Server's identifier limit would be truncated into something another competitor
/// could reach.
/// </para>
/// </remarks>
public sealed class SessionDatabaseNamingTests
{
    [Theory]
    [InlineData("session-1", "joe-doe", "session-1-joe-doe")]
    [InlineData("session-1", "john-doe", "session-1-john-doe")]
    [InlineData("skill09", "c01", "skill09-c01")]
    [InlineData("skill09", "a.b_c-d", "skill09-a.b_c-d")]
    public void TheDatabaseIsTheBaseNameFollowedByTheUsername(
        string baseName, string username, string expected) =>
        Assert.Equal(expected, SessionDatabaseNaming.For(baseName, username));

    [Fact]
    public void TwoCompetitorsNeverShareADatabase()
    {
        var joe = SessionDatabaseNaming.For("session-1", "joe-doe");
        var john = SessionDatabaseNaming.For("session-1", "john-doe");

        Assert.NotEqual(joe, john);
    }

    [Fact]
    public void TheSameCompetitorAlwaysGetsTheSameName() =>
        // Provisioning has to be restartable: a re-run that computed a different name would create a second
        // database and leave the competitor's work in the first.
        Assert.Equal(
            SessionDatabaseNaming.For("session-1", "c01"),
            SessionDatabaseNaming.For("session-1", "c01"));

    [Fact]
    public void ANameThatFitsTheIdentifierLimitIsAccepted()
    {
        var baseName = new string('b', SessionDatabaseNaming.MaxBaseNameLength);
        var username = new string(
            'u', SessionDatabaseNaming.MaxIdentifierLength - SessionDatabaseNaming.MaxBaseNameLength - 1);

        Assert.True(SessionDatabaseNaming.FitsAnIdentifier(baseName, username));
        Assert.Equal(
            SessionDatabaseNaming.MaxIdentifierLength,
            SessionDatabaseNaming.For(baseName, username).Length);
    }

    [Fact]
    public void OneCharacterMoreThanTheIdentifierLimitIsRefused()
    {
        var baseName = new string('b', SessionDatabaseNaming.MaxBaseNameLength);
        var username = new string(
            'u', SessionDatabaseNaming.MaxIdentifierLength - SessionDatabaseNaming.MaxBaseNameLength);

        Assert.False(SessionDatabaseNaming.FitsAnIdentifier(baseName, username));
    }

    [Fact]
    public void TheBaseNameBudgetLeavesRoomForEveryRealisticUsername()
    {
        // The validator caps the base name; the rest of the identifier is what a username may occupy. The
        // usernames a competition actually uses are of the order of "c01" or "joe-doe", so the remainder
        // being this generous is what makes the per-competitor check a guard rather than a routine refusal.
        var remaining = SessionDatabaseNaming.MaxIdentifierLength
                        - SessionDatabaseNaming.MaxBaseNameLength
                        - 1;

        Assert.True(remaining >= 63);
        Assert.True(SessionDatabaseNaming.FitsAnIdentifier(
            new string('b', SessionDatabaseNaming.MaxBaseNameLength), "joe-doe"));
    }
}
