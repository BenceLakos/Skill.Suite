using Skill.Suite.Domain.Sessions;
using Xunit;

namespace Skill.Suite.Application.Tests.Sessions;

/// <summary>
/// Which status a session may move to from which, and what each move is guarded by.
/// </summary>
/// <remarks>
/// The status is not decoration: the webhook only accepts a push for an Active session, so a transition that
/// is allowed when it should not be, or refused when it should not be, is the difference between a
/// competitor's submission being marked and being dropped. Stopping in particular has to stay reversible —
/// it is the pause in a competition and the repair path for a half-finished start — while closing must not.
/// </remarks>
public sealed class SessionStatusTransitionTests
{
    private static Session Draft() =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: "judge:1",
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            databaseSeedScript: null,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

    private static Session Active()
    {
        var session = Draft();
        session.Start([1, 2, 3]);
        return session;
    }

    [Fact]
    public void StoppingAnActiveSessionSuspendsIt()
    {
        var session = Active();

        Assert.True(session.Stop().IsSuccess);
        Assert.Equal(SessionStatus.Stopped, session.Status);
    }

    [Fact]
    public void StoppingADraftSessionIsRejected()
    {
        // Nothing has been provisioned, so there is no access to revoke and no service to stop.
        var session = Draft();

        var stopped = session.Stop();

        Assert.True(stopped.IsFailure);
        Assert.Equal(SessionErrors.NotActive, stopped.Error);
        Assert.Equal(SessionStatus.Draft, session.Status);
    }

    [Fact]
    public void StoppingAClosedSessionIsRejected()
    {
        var session = Active();
        session.Close();

        var stopped = session.Stop();

        Assert.True(stopped.IsFailure);
        Assert.Equal(SessionErrors.NotActive, stopped.Error);
        Assert.Equal(SessionStatus.Closed, session.Status);
    }

    [Fact]
    public void StoppingAnAlreadyStoppedSessionIsRejected()
    {
        var session = Active();
        session.Stop();

        Assert.Equal(SessionErrors.NotActive, session.Stop().Error);
    }

    [Fact]
    public void StartingAStoppedSessionResumesIt()
    {
        // The whole point of stopping rather than closing: starting again restores the repository access and
        // the services it withdrew.
        var session = Active();
        session.Stop();

        Assert.True(session.Start([4, 5, 6]).IsSuccess);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal([4, 5, 6], session.WebhookSecret);
    }

    [Fact]
    public void ClosingAStoppedSessionEndsIt()
    {
        // A session that was stopped for the night and never resumed still has to be closable.
        var session = Active();
        session.Stop();

        Assert.True(session.Close().IsSuccess);
        Assert.Equal(SessionStatus.Closed, session.Status);
    }
}
