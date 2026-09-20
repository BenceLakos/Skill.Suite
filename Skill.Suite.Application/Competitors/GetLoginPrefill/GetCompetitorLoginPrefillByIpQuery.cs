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
/// The address as the request arrived from, which is <c>HttpContext.Connection.RemoteIpAddress</c>. Forwarded
/// headers are resolved by the pipeline before that, and only for proxies the deployment trusts, so nothing
/// a client can set reaches this.
/// </param>
public sealed record GetCompetitorLoginPrefillByIpQuery(string? IpAddress)
    : IRequest<Result<CompetitorLoginPrefillDto?>>;
