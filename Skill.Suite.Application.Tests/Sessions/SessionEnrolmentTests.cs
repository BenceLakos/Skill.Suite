using Skill.Suite.Domain.Sessions;
using Xunit;

namespace Skill.Suite.Application.Tests.Sessions;

/// <summary>
/// Enrolment behaviour the provisioning handler relies on when Start is run more than once.
/// </summary>
/// <remarks>
/// The handler decides whether to call <c>db.Add</c> by checking membership before enrolling. That check is
/// load-bearing: adding an already-tracked row again makes EF attempt a duplicate insert, and skipping it for
/// a genuinely new row is the bug this suite exists to prevent — the row is never written, and every later
/// push is rejected as CompetitorNotFound while provisioning reports success.
/// </remarks>
public sealed class SessionEnrolmentTests
{
    private static Session Draft() =>
        Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: "/starter", judgementImage: "judge:1",
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

    [Fact]
    public void EnrollingACompetitorAddsExactlyOneRow()
    {
        var session = Draft();
        var competitor = Guid.NewGuid();

        var enrolment = session.EnrolCompetitor(competitor, "alice");

        Assert.Single(session.Competitors);
        Assert.Same(enrolment, session.Competitors[0]);
        Assert.Equal("alice", enrolment.RepositoryName);
        Assert.Equal(SessionProvisionStatus.Pending, enrolment.ProvisionStatus);
        Assert.NotEqual(Guid.Empty, enrolment.Id);
    }

    [Fact]
    public void ReEnrollingReturnsTheSameRowInsteadOfDuplicatingIt()
    {
        // A second row for the same repository would make the webhook's lookup ambiguous, and the unique
        // index on (SessionId, RepositoryName) would fail the whole save.
        var session = Draft();
        var competitor = Guid.NewGuid();

        var first = session.EnrolCompetitor(competitor, "alice");
        first.MarkProvisioned("http://gitea/round-1/alice.git");

        var second = session.EnrolCompetitor(competitor, "alice");

        Assert.Same(first, second);
        Assert.Single(session.Competitors);
    }

    [Fact]
    public void ReEnrollingResetsAFailedRowToPendingSoTheRetryIsVisible()
    {
        var session = Draft();
        var competitor = Guid.NewGuid();

        session.EnrolCompetitor(competitor, "alice").MarkFailed("the git host returned 500");
        var retried = session.EnrolCompetitor(competitor, "alice");

        Assert.Equal(SessionProvisionStatus.Pending, retried.ProvisionStatus);
        Assert.Null(retried.ProvisionError);
        Assert.Null(retried.ProvisionedAt);
    }

    [Fact]
    public void ProvisionErrorIsTruncatedToTheColumnWidth()
    {
        // The message comes from a git-host response body, which can exceed the column and would otherwise
        // fail the save with a value-too-long error instead of recording why provisioning failed.
        var session = Draft();
        var enrolment = session.EnrolCompetitor(Guid.NewGuid(), "alice");

        enrolment.MarkFailed(new string('x', SessionCompetitor.MaxErrorLength + 500));

        Assert.Equal(SessionCompetitor.MaxErrorLength, enrolment.ProvisionError!.Length);
    }

    [Fact]
    public void StartingAnAlreadyActiveSessionIsAllowedSoProvisioningCanBeResumed()
    {
        var session = Draft();

        Assert.True(session.Start([1, 2, 3]).IsSuccess);
        Assert.Equal(SessionStatus.Active, session.Status);

        // The repair path: a session left Active by a half-finished provisioning run must be startable again.
        var again = session.Start([4, 5, 6]);

        Assert.True(again.IsSuccess);
        Assert.Equal([4, 5, 6], session.WebhookSecret);
    }

    [Fact]
    public void StartingAClosedSessionIsRejected()
    {
        var session = Draft();
        session.Close();

        Assert.True(session.Start([1, 2, 3]).IsFailure);
    }

    [Fact]
    public void StartingWithoutATemplateFolderIsRejected()
    {
        var session = Session.Create(
            "Round 1", "round-1", null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
            templateFolder: null, judgementImage: "judge:1",
            databaseName: null, databaseReadAccess: true, databaseWriteAccess: true,
            gitCredentialId: Guid.NewGuid(), judgementImagePullCredentialId: null,
            dockerImages: []).Value;

        Assert.Equal(SessionErrors.MissingTemplateFolder, session.Start([1, 2, 3]).Error);
    }

    [Fact]
    public void OrganizationNameIsTheSlug() =>
        // The webhook resolves a push to a session by matching the repository owner against this.
        Assert.Equal("round-1", Draft().GitOrganization);
}
