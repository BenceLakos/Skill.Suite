using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors.Events;

namespace Skill.Suite.Domain.Competitors;

public sealed class Competitor : AuditableEntity<Guid>
{
    private Competitor() { }

    public string Username { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public byte[] EncryptedPassword { get; private set; } = [];
    public string IpAddress { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;

    public static Competitor Create(string username, string fullName, byte[] encryptedPassword, string ipAddress, string countryCode)
    {
        var competitor = new Competitor
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            FullName = fullName.Trim(),
            EncryptedPassword = encryptedPassword,
            IpAddress = ipAddress.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
        };

        competitor.RaiseDomainEvent(new CompetitorCreatedEvent(competitor.Id));
        return competitor;
    }

    public void UpdateProfile(string username, string fullName, string ipAddress, string countryCode)
    {
        Username = username.Trim();
        FullName = fullName.Trim();
        IpAddress = ipAddress.Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();

        RaiseDomainEvent(new CompetitorUpdatedEvent(Id));
    }

    public void SetPassword(byte[] encryptedPassword)
    {
        EncryptedPassword = encryptedPassword;
        RaiseDomainEvent(new CompetitorUpdatedEvent(Id));
    }

    public void MarkRemoved() =>
        RaiseDomainEvent(new CompetitorRemovedEvent(Id));
}
