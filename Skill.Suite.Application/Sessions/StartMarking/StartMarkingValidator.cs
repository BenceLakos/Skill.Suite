using FluentValidation;
using Skill.Suite.Application.Competitors.Validation;

namespace Skill.Suite.Application.Sessions.StartMarking;

/// <summary>
/// The marking machine's address is required and has to be an address.
/// </summary>
/// <remarks>
/// Checked with the same rule a competitor's own workstation address is, because it is used for the same
/// thing: it goes verbatim into the proxy's <c>ClientIP</c> matcher. Traefik does not reject a rule it
/// cannot parse — the router simply never matches — so an address that is a typo produces marking
/// containers that are running, healthy and unreachable.
/// </remarks>
public sealed class StartMarkingValidator : AbstractValidator<StartMarkingCommand>
{
    public StartMarkingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CompetitorId).NotEmpty();

        RuleFor(x => x.MarkingIpAddress)
            .NotEmpty()
            .WithMessage("Enter the IP address of the computer the marking will be done from.")
            .Must(CompetitorRules.IsValidIpAddress)
            .WithMessage("The marking computer's address must be a valid IPv4 or IPv6 address.");
    }
}
