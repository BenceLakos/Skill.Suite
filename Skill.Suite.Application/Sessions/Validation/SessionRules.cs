using System.Text.RegularExpressions;
using FluentValidation;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.Validation;

internal static class SessionRules
{
    public static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, List<SessionDockerImage>> ValidDockerImages<T>(this IRuleBuilder<T, List<SessionDockerImage>> rule) =>
        rule.NotNull().Must(images => images.All(i =>
            !string.IsNullOrWhiteSpace(i.Image)
            && i.PortMappings.All(p => p.HostPort is > 0 and < 65536 && p.ContainerPort is > 0 and < 65536)
            && i.Volumes.All(v => !string.IsNullOrWhiteSpace(v.HostPath) && !string.IsNullOrWhiteSpace(v.ContainerPath))))
            .WithMessage("Each docker image needs a non-empty image name, valid port mappings (1-65535) and non-empty volume paths.");
}
