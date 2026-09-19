using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.DockerImages;

public static class DockerImageErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("DockerImage.NotFound", $"Docker image '{id}' was not found.");

    public static readonly Error NameConflict =
        Error.Conflict("DockerImage.NameConflict", "A docker image with that name already exists.");

    public static readonly Error MissingNexusCredential =
        Error.Validation("DockerImage.MissingNexusCredential",
            "This docker image has no Nexus credential configured; cannot push.");

    public static readonly Error PushNotAllowedForPulledImage =
        Error.Validation("DockerImage.PushNotAllowedForPulledImage",
            "Pulled images come from an external registry and cannot be pushed from here.");

    public static Error RegistryUnavailable(string detail) =>
        Error.Failure("DockerImage.RegistryUnavailable",
            $"The container registry could not be listed. {detail}");
}
