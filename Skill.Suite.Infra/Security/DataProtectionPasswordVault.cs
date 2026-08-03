using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Security;

internal sealed class DataProtectionPasswordVault : IPasswordVault
{
    private const string ProtectorPurpose = "Skill.Suite.Competitor.Password.v1";

    private readonly IDataProtector _protector;

    public DataProtectionPasswordVault(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);
    }

    public byte[] Protect(string plaintext) =>
        _protector.Protect(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(byte[] ciphertext) =>
        Encoding.UTF8.GetString(_protector.Unprotect(ciphertext));
}
