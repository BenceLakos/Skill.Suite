using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors.Events;

namespace Skill.Suite.Domain.Competitors;

public sealed class Competitor : AuditableEntity<Guid>
{
    private Competitor() { }

    public string Username { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public byte[] EncryptedPassword { get; private set; } = [];
    /// <summary>The workstation the competitor works at, which identifies them on the venue network.</summary>
    public string IpAddress { get; private set; } = string.Empty;

    /// <summary>
    /// A second device of theirs — a phone or tablet — or null when they have none.
    /// </summary>
    /// <remarks>
    /// Optional because most competitions do not hand one out, and a second address is only ever an addition
    /// to <see cref="IpAddress"/>: wherever the platform recognises a competitor by where they are sitting —
    /// pre-filling the sign-in form, routing them to their own service container — this address counts as
    /// the same person. It is therefore required to differ from <see cref="IpAddress"/>, since two names for
    /// one machine would buy nothing and read as a mistake.
    /// </remarks>
    public string? MobileIpAddress { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public static Competitor Create(
        string username,
        string fullName,
        byte[] encryptedPassword,
        string ipAddress,
        string? mobileIpAddress,
        string countryCode)
    {
        var competitor = new Competitor
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            FullName = fullName.Trim(),
            EncryptedPassword = encryptedPassword,
            IpAddress = ipAddress.Trim(),
            MobileIpAddress = NormalizeOptional(mobileIpAddress),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
        };

        competitor.RaiseDomainEvent(new CompetitorCreatedEvent(competitor.Id));
        return competitor;
    }

    public void UpdateProfile(
        string username, string fullName, string ipAddress, string? mobileIpAddress, string countryCode)
    {
        Username = username.Trim();
        FullName = fullName.Trim();
        IpAddress = ipAddress.Trim();
        MobileIpAddress = NormalizeOptional(mobileIpAddress);
        CountryCode = countryCode.Trim().ToUpperInvariant();

        RaiseDomainEvent(new CompetitorUpdatedEvent(Id));
    }

    /// <summary>
    /// Blank becomes absent, so "they have no second device" has one representation.
    /// </summary>
    /// <remarks>
    /// An empty string would reach the proxy as <c>ClientIP(``)</c>, a rule that matches nothing and takes
    /// the whole route down with it.
    /// </remarks>
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void SetPassword(byte[] encryptedPassword)
    {
        EncryptedPassword = encryptedPassword;
        RaiseDomainEvent(new CompetitorUpdatedEvent(Id));
    }

    public void MarkRemoved() =>
        RaiseDomainEvent(new CompetitorRemovedEvent(Id));
}
