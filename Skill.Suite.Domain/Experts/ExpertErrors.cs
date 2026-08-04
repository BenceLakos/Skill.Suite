using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Experts;

public static class ExpertErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Expert.NotFound", $"Expert '{id}' was not found.");
}
