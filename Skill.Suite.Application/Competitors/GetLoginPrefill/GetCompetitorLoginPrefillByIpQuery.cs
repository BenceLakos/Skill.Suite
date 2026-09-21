namespace Skill.Suite.Application.Competitors.GetLoginPrefill;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// The competitor sitting at <paramref name="IpAddress"/>, for pre-filling the sign-in form.
/// </summary>
/// <remarks>
/// Deliberately carries no validator. An absent, empty or unparseable address is a legitimate "nobody is
/// recognised here" — the query is issued for every anonymous visit to the sign-in page, including ones from
/// a machine nobody recorded — and turning that into a validation failure would make the page swallow an
/// error on its most common path.
/// </remarks>
/// <param name="IpAddress">
/// The address the request arrived from, which is <c>HttpContext.Connection.RemoteIpAddress</c> after the
/// pipeline has applied <c>X-Forwarded-For</c>. The deployment accepts that header from any peer, so on a
/// network less private than a competition venue's this is a claim rather than an observation.
/// </param>
public sealed record GetCompetitorLoginPrefillByIpQuery(string? IpAddress)
    : IRequest<Result<CompetitorLoginPrefillDto?>>;
