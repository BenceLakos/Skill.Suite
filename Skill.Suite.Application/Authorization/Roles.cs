namespace Skill.Suite.Application.Authorization;

public static class Roles
{
    public const string Admin = nameof(Admin);
    public const string Expert = nameof(Expert);
    public const string Competitor = nameof(Competitor);

    public static readonly IReadOnlyList<string> All = [Admin, Expert, Competitor];
}
