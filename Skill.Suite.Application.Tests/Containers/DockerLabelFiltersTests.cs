namespace Skill.Suite.Application.Tests.Containers;

using Skill.Suite.Infra.Containers;
using Xunit;

/// <summary>
/// The filters that decide which containers get force-removed.
/// </summary>
/// <remarks>
/// Asserted directly because a mistake here does not fail — it widens. A filter that silently stopped being
/// emitted would match more containers than the caller meant, and the caller's next step is
/// <c>docker rm --force</c>: one competitor's marking run stopped would take down every expert's.
/// </remarks>
public sealed class DockerLabelFiltersTests
{
    [Fact]
    public void OneLabelIsOneFilter()
    {
        var args = DockerLabelFilters.ListArguments(
            new Dictionary<string, string> { ["skill-suite.session"] = "round-1" });

        Assert.Equal(
            ["ps", "-a", "-q", "--filter", "label=skill-suite.session=round-1"],
            args);
    }

    [Fact]
    public void TwoLabelsAreTwoFiltersWhichDockerReadsAsAnAnd()
    {
        // Exactly the narrowing removing ONE competitor's marking containers needs: the marking label says
        // which session, the competitor label says whose, and either alone is too wide.
        var args = DockerLabelFilters.ListArguments(new Dictionary<string, string>
        {
            ["skill-suite.marking"] = "round-1",
            ["skill-suite.competitor"] = "c01",
        });

        Assert.Equal(
            [
                "ps", "-a", "-q",
                "--filter", "label=skill-suite.competitor=c01",
                "--filter", "label=skill-suite.marking=round-1",
            ],
            args);
    }

    [Fact]
    public void TheOrderOfTheFiltersDoesNotDependOnTheOrderTheyWereGivenIn()
    {
        // Sorted so the argument list is the same whichever way the caller built the dictionary, which is
        // what makes it assertable at all.
        var one = DockerLabelFilters.ListArguments(new Dictionary<string, string>
        {
            ["skill-suite.marking"] = "round-1",
            ["skill-suite.competitor"] = "c01",
        });

        var other = DockerLabelFilters.ListArguments(new Dictionary<string, string>
        {
            ["skill-suite.competitor"] = "c01",
            ["skill-suite.marking"] = "round-1",
        });

        Assert.Equal(one, other);
    }

    [Fact]
    public void NoLabelsProducesNoFiltersAtAll()
    {
        // The manager refuses this case before it gets here — `docker ps -aq` with no filter lists every
        // container on the host, including the platform's own, the database and the git server.
        Assert.Equal(["ps", "-a", "-q"], DockerLabelFilters.ListArguments(new Dictionary<string, string>()));
    }

    [Fact]
    public void TheExpressionsAreTheFiltersWithoutTheFlags()
    {
        Assert.Equal(
            ["label=skill-suite.session=round-1"],
            DockerLabelFilters.Expressions(
                new Dictionary<string, string> { ["skill-suite.session"] = "round-1" }));
    }
}
