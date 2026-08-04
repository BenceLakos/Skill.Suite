using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Verifies a git host's HMAC signature over the raw webhook body.
/// </summary>
/// <remarks>
/// Gitea signs the exact request bytes with the secret configured on the hook and sends the result as a bare
/// lowercase hex digest in <c>X-Gitea-Signature</c>, duplicating it as <c>sha256=&lt;hex&gt;</c> in
/// <c>X-Hub-Signature-256</c> for GitHub compatibility. Both forms are accepted here; the <c>sha1</c>
/// variant Gitea also sends is not, because SHA-1 is not worth verifying against.
/// <para>
/// The signature covers the bytes as received, so the body must not be deserialized and re-serialized before
/// it gets here — a re-encode changes whitespace and key order and every signature fails.
/// </para>
/// </remarks>
public static class WebhookSignature
{
    private const string Sha256Prefix = "sha256=";

    /// <summary>Whether <paramref name="header"/> is a valid signature for <paramref name="body"/>.</summary>
    public static bool IsValid(ReadOnlySpan<byte> body, string? header, string secret)
    {
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrEmpty(secret))
            return false;

        var provided = header.Trim();
        if (provided.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
            provided = provided[Sha256Prefix.Length..];

        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, expected);

        Span<byte> actual = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (!TryDecodeHex(provided, actual))
            return false;

        // Constant-time: a length-independent early exit here would leak the digest one byte at a time to
        // anyone able to time the endpoint.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static bool TryDecodeHex(string hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2)
            return false;

        return Convert.FromHexString(hex, destination, out _, out var written) == OperationStatus.Done
               && written == destination.Length;
    }
}
