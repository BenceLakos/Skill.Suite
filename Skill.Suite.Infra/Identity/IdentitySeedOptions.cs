namespace Skill.Suite.Infra.Identity;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Seed";

    public SeedAdminOptions? Admin { get; set; }
}

public sealed class SeedAdminOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
}
