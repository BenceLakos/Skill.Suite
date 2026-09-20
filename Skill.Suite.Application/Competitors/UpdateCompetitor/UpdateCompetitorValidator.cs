using FluentValidation;
using Skill.Suite.Application.Competitors.Validation;

namespace Skill.Suite.Application.Competitors.UpdateCompetitor;

public sealed class UpdateCompetitorValidator : AbstractValidator<UpdateCompetitorCommand>
{
    public UpdateCompetitorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(120)
            .Must(u => CompetitorRules.UsernamePattern.IsMatch(u))
            .WithMessage("Username may contain only letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.IpAddress)
            .NotEmpty()
            .MaximumLength(45)
            .Must(CompetitorRules.IsValidIpAddress)
            .WithMessage("IP address must be a valid IPv4 or IPv6 address.");

        RuleFor(x => x.MobileIpAddress).ValidMobileIpAddress(x => x.IpAddress);

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Must(c => CompetitorRules.CountryCodePattern.IsMatch(c))
            .WithMessage("Country code must be an ISO 3166-1 alpha-3 code (3 letters).");
    }
}
