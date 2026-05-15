using Microsoft.AspNetCore.Identity;

namespace Skill.Suite.Infra.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
}
