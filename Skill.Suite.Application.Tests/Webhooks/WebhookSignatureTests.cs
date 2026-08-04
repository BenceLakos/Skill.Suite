using System.Security.Cryptography;
using System.Text;
using Skill.Suite.Application.Webhooks;
using Xunit;

namespace Skill.Suite.Application.Tests.Webhooks;

public sealed class WebhookSignatureTests
{
    private const string Secret = "topsecret-abc123";

    /// <summary>
    /// A real delivery captured from Gitea 1.26, with the digest it actually sent.
    /// </summary>
    /// <remarks>
    /// The body is shortened here, and the expected digest recomputed from it, so this pins the algorithm and
    /// header format rather than one specific payload. The captured header was a bare lowercase hex digest —
    /// no <c>sha256=</c> prefix — which is the detail worth not rediscovering.
    /// </remarks>
    private static readonly byte[] Body =
        Encoding.UTF8.GetBytes("""{"ref":"refs/heads/main","after":"9d71157d9ece123f0065b78e2dd0f5766009d6e9"}""");

    private static string ValidSignature() =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Body));

    [Fact]
    public void BareHexDigest_IsAccepted() =>
        Assert.True(WebhookSignature.IsValid(Body, ValidSignature(), Secret));

    [Fact]
    public void UppercaseHexDigest_IsAccepted() =>
        Assert.True(WebhookSignature.IsValid(Body, ValidSignature().ToUpperInvariant(), Secret));

    [Fact]
    public void GitHubStylePrefixedDigest_IsAccepted() =>
        Assert.True(WebhookSignature.IsValid(Body, $"sha256={ValidSignature()}", Secret));

    [Fact]
    public void SurroundingWhitespace_IsTolerated() =>
        Assert.True(WebhookSignature.IsValid(Body, $"  {ValidSignature()}  ", Secret));

    [Fact]
    public void WrongSecret_IsRejected() =>
        Assert.False(WebhookSignature.IsValid(Body, ValidSignature(), "not-the-secret"));

    [Fact]
    public void ModifiedBody_IsRejected()
    {
        var signature = ValidSignature();
        var tampered = Encoding.UTF8.GetBytes(
            """{"ref":"refs/heads/main","after":"0000000000000000000000000000000000000000"}""");

        Assert.False(WebhookSignature.IsValid(tampered, signature, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-hex-at-all")]
    [InlineData("sha256=")]
    // One character short: the hex decode must reject it rather than compare a truncated digest.
    [InlineData("0a468a64722abb84ae2ab53cb6d601ac4108ea19980ed5e620a9158956a0eb0")]
    // A SHA-1 digest, which Gitea also sends in X-Hub-Signature. Wrong length, so it cannot be confused.
    [InlineData("sha1=ee024ea5dec3f92751ed22d259790bbccd24932d")]
    public void MissingOrMalformedSignature_IsRejected(string? header) =>
        Assert.False(WebhookSignature.IsValid(Body, header, Secret));

    [Fact]
    public void EmptySecret_IsRejected() =>
        // A session whose secret never got written must not accidentally verify. The handler decides whether
        // to allow an unsigned push; this must never claim one was signed.
        Assert.False(WebhookSignature.IsValid(Body, ValidSignature(), string.Empty));
}
