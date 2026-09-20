namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions;
using Xunit;

/// <summary>
/// The figures the session progress bar is drawn from, for both starting and stopping.
/// </summary>
/// <remarks>
/// The bar is the only thing the admin has to tell a slow git host from a stuck one, so the two ways it can
/// lie are worth pinning down: a percentage computed from a total of zero, and a bar that reads as finished
/// while repositories are still being created.
/// </remarks>
public sealed class SessionProvisioningProgressTests
{
    [Fact]
    public void PercentIsTheCompletedShareOfTheTotal()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 7, 20);

        Assert.Equal(35, progress.Percent);
    }

    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(1, 6, 17)]
    public void PercentRoundsToTheNearestWholePercent(int completed, int total, int expected)
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, completed, total);

        Assert.Equal(expected, progress.Percent);
    }

    [Fact]
    public void AStageWithWorkLeftNeverReadsAsFinished()
    {
        // Rounding alone would report 100% here, with a repository still to create.
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 199, 200);

        Assert.Equal(99, progress.Percent);
    }

    [Fact]
    public void PercentIsZeroBeforeAnythingIsDone()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 0, 20);

        Assert.Equal(0, progress.Percent);
        Assert.False(progress.IsIndeterminate);
    }

    [Fact]
    public void PercentIsFullWhenEveryCompetitorIsDealtWith()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 20, 20);

        Assert.Equal(100, progress.Percent);
    }

    [Fact]
    public void PercentNeverExceedsTheFullBar()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 25, 20);

        Assert.Equal(100, progress.Percent);
    }

    [Fact]
    public void AZeroTotalIsIndeterminateRatherThanADivisionByZero()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Preparing, 0, 0);

        Assert.True(progress.IsIndeterminate);
        Assert.Equal(0, progress.Percent);
    }

    [Fact]
    public void AnIndeterminateStageCarriesNoCounts()
    {
        var progress = SessionProvisioningProgress.Indeterminate(SessionProvisioningStage.Preparing);

        Assert.Equal(SessionProvisioningStage.Preparing, progress.Stage);
        Assert.Equal(0, progress.Completed);
        Assert.Equal(0, progress.Total);
        Assert.True(progress.IsIndeterminate);
    }

    [Fact]
    public void ACountedStageIsNotIndeterminate()
    {
        var progress = new SessionProvisioningProgress(SessionProvisioningStage.Repositories, 0, 1);

        Assert.False(progress.IsIndeterminate);
    }
}
