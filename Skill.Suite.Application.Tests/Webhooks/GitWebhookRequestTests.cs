using System.Text.Json;
using Skill.Suite.Application.Webhooks;
using Xunit;

namespace Skill.Suite.Application.Tests.Webhooks;

/// <summary>
/// Field extraction from an inbound push payload — the part that decides which session and which competitor
/// a submission belongs to.
/// </summary>
public sealed class GitWebhookRequestTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static GitWebhookRequest Parse(string json) =>
        JsonSerializer.Deserialize<GitWebhookRequest>(json, Options)!;

    /// <summary>Shaped exactly like the Gitea 1.26 delivery captured while building this.</summary>
    private const string GiteaPush = """
        {
          "ref": "refs/heads/main",
          "after": "9d71157d9ece123f0065b78e2dd0f5766009d6e9",
          "repository": {
            "name": "alice",
            "full_name": "greenwave-round1/alice",
            "clone_url": "http://localhost:3000/greenwave-round1/alice.git",
            "owner": { "username": "greenwave-round1", "login": "greenwave-round1" }
          }
        }
        """;

    [Fact]
    public void GiteaPush_ResolvesOwnerRepositoryBranchAndSha()
    {
        var push = Parse(GiteaPush);

        Assert.Equal("greenwave-round1", push.ResolveOwner());
        Assert.Equal("alice", push.ResolveRepositorySlug());
        Assert.Equal("main", push.ResolveBranch());
        Assert.Equal("9d71157d9ece123f0065b78e2dd0f5766009d6e9", push.ResolveCommitSha());
        Assert.Equal("http://localhost:3000/greenwave-round1/alice.git", push.ResolveRepositoryUrl());
        Assert.True(push.IsBranchPush());
    }

    [Fact]
    public void OwnerFallsBackToTheFirstSegmentOfFullName()
    {
        // GitHub-style payloads and hand-rolled ones may omit the owner object entirely.
        var push = Parse("""{"repository":{"full_name":"greenwave-round1/bob","clone_url":"http://x/y.git"}}""");

        Assert.Equal("greenwave-round1", push.ResolveOwner());
        Assert.Equal("bob", push.ResolveRepositorySlug());
    }

    [Fact]
    public void OwnerIsNullWhenNothingIdentifiesIt() =>
        Assert.Null(Parse("""{"repository":{"name":"alice","clone_url":"http://x/y.git"}}""").ResolveOwner());

    [Fact]
    public void RepositorySlugStripsTheGitSuffixAndOwnerPath() =>
        // Only reachable through the flat repository_name field, which hand-rolled senders use.
        Assert.Equal("alice", Parse("""{"repository_name":"greenwave-round1/alice.git"}""").ResolveRepositorySlug());

    [Fact]
    public void LoginIsUsedWhenUsernameIsAbsent() =>
        Assert.Equal("org", Parse("""{"repository":{"owner":{"login":"org"}}}""").ResolveOwner());

    [Fact]
    public void TagPushIsNotABranchPush()
    {
        // Left unfiltered this reaches git clone --branch refs/tags/v1 and fails with a raw git error.
        var push = Parse("""{"ref":"refs/tags/v1.0.0","repository":{"name":"alice"}}""");

        Assert.False(push.IsBranchPush());
    }

    [Fact]
    public void PayloadWithoutARefIsTreatedAsABranchPush() =>
        // Hand-rolled senders post `branch` instead of `ref`; rejecting those would break the smoke path.
        Assert.True(Parse("""{"branch":"main","repository":{"name":"alice"}}""").IsBranchPush());
}
