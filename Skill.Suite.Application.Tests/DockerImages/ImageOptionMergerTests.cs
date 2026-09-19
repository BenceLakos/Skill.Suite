namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// How the registered images and the registry's images become one list.
/// </summary>
/// <remarks>
/// The case that matters is the overlap. A judge image is normally both registered in the database and
/// present on the registry, and the two spellings differ — the registry's is lower-cased — so a naive
/// concatenation shows the same image twice and leaves an operator choosing between them.
/// </remarks>
public sealed class ImageOptionMergerTests
{
    [Fact]
    public void BothSourcesEndUpInOneSortedList() =>
        Assert.Equal(
            ["alpine:3.20", "localhost:3000/admin/judge:1.0", "postgres:16"],
            ImageOptionMerger.Merge(["postgres:16", "alpine:3.20"], ["localhost:3000/admin/judge:1.0"]));

    [Fact]
    public void ADuplicateAppearsOnceWithThePreconfiguredSpelling() =>
        // Preconfigured wins because it is what an operator typed and what the sessions already reference.
        Assert.Equal(
            ["localhost:3000/Admin/Judge:1.0"],
            ImageOptionMerger.Merge(
                ["localhost:3000/Admin/Judge:1.0"],
                ["localhost:3000/admin/judge:1.0"]));

    [Fact]
    public void BlanksAreDroppedAndSurroundingSpaceIsTrimmed() =>
        Assert.Equal(
            ["alpine:3.20", "postgres:16"],
            ImageOptionMerger.Merge(["  postgres:16  ", "", "   "], ["alpine:3.20"]));

    [Fact]
    public void NothingDiscoveredStillLeavesThePreconfiguredImages() =>
        // The registry being unreachable must not empty the dropdown.
        Assert.Equal(
            ["alpine:3.20", "postgres:16"],
            ImageOptionMerger.Merge(["postgres:16", "alpine:3.20"], []));
}
