using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials.Events;

namespace Skill.Suite.Domain.Credentials;

public sealed class Credential : AuditableEntity<Guid>
{
    private Credential() { }

    public string Name { get; private set; } = string.Empty;
    public CredentialKind Kind { get; private set; }
    public byte[] EncryptedSecret { get; private set; } = [];

    public static Credential Create(string name, CredentialKind kind, byte[] encryptedSecret)
    {
        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Kind = kind,
            EncryptedSecret = encryptedSecret,
        };

        credential.RaiseDomainEvent(new CredentialCreatedEvent(credential.Id));
        return credential;
    }

    public void Update(string name, CredentialKind kind, byte[] encryptedSecret)
    {
        Name = name.Trim();
        Kind = kind;
        EncryptedSecret = encryptedSecret;
        RaiseDomainEvent(new CredentialUpdatedEvent(Id));
    }

    public void MarkRemoved() =>
        RaiseDomainEvent(new CredentialRemovedEvent(Id));
}
