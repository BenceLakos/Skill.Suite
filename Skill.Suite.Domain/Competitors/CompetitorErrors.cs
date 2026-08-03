using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Competitors;

public static class CompetitorErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Competitor.NotFound", $"Competitor '{id}' was not found.");

    public static readonly Error UsernameConflict =
        Error.Conflict("Competitor.UsernameConflict", "A competitor with that username already exists.");

    public static Error UserProvisioningFailed(string detail) =>
        Error.Failure("Competitor.UserProvisioningFailed", $"Failed to provision login account: {detail}");
}
