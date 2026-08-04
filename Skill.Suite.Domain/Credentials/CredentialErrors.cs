using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Credentials;

public static class CredentialErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Credential.NotFound", $"Credential '{id}' was not found.");

    public static readonly Error NameConflict =
        Error.Conflict("Credential.NameConflict", "A credential with that name already exists.");
}
