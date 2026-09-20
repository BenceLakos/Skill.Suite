namespace Skill.Suite.Application.Competitors.GetLoginPrefill;

using System.Security.Cryptography;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

/// <summary>
/// Reads back the credentials of the competitor whose workstation address the request came from.
/// </summary>
/// <remarks>
/// Competitors sit at fixed workstations for the length of a session, so the address identifies the person as
/// reliably as the badge on the desk does — which is what makes handing them their own password on the sign-in
/// page a convenience rather than a disclosure.
/// <para>
/// Two competitors recorded at one address produce NO pre-fill, not the first of them. There is no unique
/// index standing behind the address, so the duplicate is an ordinary data-entry mistake, and filling in
/// competitor A's password on competitor B's machine is worse in every way than an empty form.
/// </para>
/// </remarks>
public sealed class GetCompetitorLoginPrefillByIpHandler(
    IAppDbContext db,
    IPasswordVault vault,
    ILogger<GetCompetitorLoginPrefillByIpHandler> logger)
    : IRequestHandler<GetCompetitorLoginPrefillByIpQuery, Result<CompetitorLoginPrefillDto?>>
{
    public async ValueTask<Result<CompetitorLoginPrefillDto?>> Handle(
        GetCompetitorLoginPrefillByIpQuery request, CancellationToken cancellationToken)
    {
        var client = WorkstationMatch.Parse(request.IpAddress);
        if (client is null)
            return NoPrefill();

        var competitors = await db.Competitors
            .AsNoTracking()
            .OrderBy(c => c.Username)
            .Select(c => new { c.Username, c.IpAddress, c.MobileIpAddress, c.EncryptedPassword })
            .ToListAsync(cancellationToken);

        // Both of a competitor's devices count as them: the phone they were handed is the same person as the
        // workstation they sit at, and the task is routinely the one to be demonstrated on it.
        var matches = WorkstationMatch.MatchIndexes(
            client,
            competitors
                .Select(c => (IReadOnlyList<string?>)[c.IpAddress, c.MobileIpAddress])
                .ToList());

        if (matches.Count == 0)
            return NoPrefill();

        if (matches.Count > 1)
        {
            logger.LogWarning(
                "{Count} competitors are recorded at workstation {Address}, so the sign-in form was not "
                + "pre-filled: {Usernames}",
                matches.Count,
                client,
                string.Join(", ", matches.Select(index => competitors[index].Username)));

            return NoPrefill();
        }

        var competitor = competitors[matches[0]];

        try
        {
            return Result.Success<CompetitorLoginPrefillDto?>(
                new CompetitorLoginPrefillDto(
                    competitor.Username, vault.Unprotect(competitor.EncryptedPassword)));
        }
        catch (CryptographicException ex)
        {
            // The one place in the application where a key ring that no longer matches the stored ciphertext
            // must not surface. A database restored next to a recreated keys volume makes every Unprotect
            // throw, and letting that escape would take the sign-in page down for everybody — including the
            // administrator who is the only one who can repair it. Nobody is recognised; everyone can still
            // type their password.
            logger.LogError(ex,
                "The stored password of {Username} could not be decrypted, so the sign-in form was not "
                + "pre-filled. The data-protection key ring does not match the stored value.",
                competitor.Username);

            return NoPrefill();
        }
    }

    /// <summary>Nobody is recognised here, which leaves the sign-in page exactly as it was.</summary>
    private static Result<CompetitorLoginPrefillDto?> NoPrefill() =>
        Result.Success<CompetitorLoginPrefillDto?>(null);
}
