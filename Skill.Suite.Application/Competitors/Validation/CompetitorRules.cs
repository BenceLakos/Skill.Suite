using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Skill.Suite.Application.Competitors.Validation;

internal static class CompetitorRules
{
    public static readonly Regex UsernamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    public static readonly Regex CountryCodePattern = new("^[A-Za-z]{3}$", RegexOptions.Compiled);

    /// <summary>The widest an address can be written: an IPv6 address with a scope id.</summary>
    public const int MaxIpAddressLength = 45;

    public static bool IsValidIpAddress(string value) => IPAddress.TryParse(value, out _);

    /// <summary>
    /// What a competitor's optional second device has to be, if they have one.
    /// </summary>
    /// <remarks>
    /// Shared by create and update because a competitor edited into a state they could not have been created
    /// in is the same broken row either way — and this one is felt at the proxy: the address goes verbatim
    /// into a <c>ClientIP</c> matcher, and Traefik answers an unparseable rule by never matching rather than
    /// by complaining.
    /// </remarks>
    /// <param name="workstation">
    /// The competitor's workstation address off the same command. Two names for one machine buy nothing and
    /// read as a mistake, so the pair is rejected rather than silently collapsed.
    /// </param>
    public static IRuleBuilderOptions<T, string?> ValidMobileIpAddress<T>(
        this IRuleBuilder<T, string?> rule, Func<T, string> workstation) =>
        rule.MaximumLength(MaxIpAddressLength)
            .Must(address => IsUnset(address) || IsValidIpAddress(address!))
            .WithMessage("The mobile device address must be a valid IPv4 or IPv6 address.")
            .Must((command, address) =>
                IsUnset(address)
                || !string.Equals(address!.Trim(), workstation(command)?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage(
                "The mobile device address must differ from the workstation address — it is a second "
                + "device, not another name for the same one.");

    private static bool IsUnset(string? value) => string.IsNullOrWhiteSpace(value);
}
