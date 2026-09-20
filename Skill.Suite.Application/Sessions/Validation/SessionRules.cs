using System.Text.RegularExpressions;
using FluentValidation;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.Validation;

internal static class SessionRules
{
    public static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>Matches the column, and the template folder path beside it.</summary>
    public const int MaxSeedScriptLength = 1000;

    /// <summary>
    /// The session's database name, which is a base rather than a database: each competitor gets
    /// <c>{base}-{username}</c>.
    /// </summary>
    /// <remarks>
    /// Shorter than the 1000-character column and shorter than the 120 this once allowed, because the name
    /// is not used on its own: a base at the old limit plus a separator plus a username left every
    /// competitor's database name past the 128 characters SQL Server allows for an identifier, which would
    /// have surfaced as N identical per-competitor failures at the worst possible moment. The column is left
    /// as it is — widening validation later costs nothing, and a migration that truncates a name somebody's
    /// databases are already called costs a competition.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> ValidDatabaseBaseName<T>(
        this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(SessionDatabaseNaming.MaxBaseNameLength)
            .WithMessage(
                "The database name is a base name each competitor's own database is derived from, so it is " +
                $"limited to {SessionDatabaseNaming.MaxBaseNameLength} characters — SQL Server allows " +
                $"{SessionDatabaseNaming.MaxIdentifierLength} for the whole name, and the competitor's " +
                "username goes on the end.");

    /// <summary>
    /// What a database seed script path has to be for the session to be worth saving.
    /// </summary>
    /// <remarks>
    /// Shared by create and update because a session that cannot be started is no better for having been
    /// edited into that state rather than created in it. The traversal rule is the same one the starter
    /// package store enforces when it reads the file — repeated here only so the administrator is told while
    /// they are still on the form.
    /// </remarks>
    /// <param name="databaseName">
    /// Reads the database name off the command being validated: a script with nothing to run it against is
    /// the one mistake this field makes that the field alone cannot show.
    /// </param>
    public static IRuleBuilderOptions<T, string?> ValidSeedScript<T>(
        this IRuleBuilder<T, string?> rule, Func<T, string?> databaseName) =>
        rule.MaximumLength(MaxSeedScriptLength)
            .Must(script => IsUnset(script) || SqlScriptFile.Matches(script!))
            .WithMessage($"The database seed script must be a {SqlScriptFile.Extension} file.")
            .Must(script => IsUnset(script) || StarterPackagePath.IsSafeRelativePath(script))
            .WithMessage(
                "The database seed script must be a path inside the starter packages volume, such as " +
                "'my-package/seed/init.sql'.")
            .Must((command, script) => IsUnset(script) || !string.IsNullOrWhiteSpace(databaseName(command)))
            .WithMessage(
                "A database seed script needs a database name to run against. Set one, or clear the script.");

    private static bool IsUnset(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// What a session's docker services have to be for the session to be worth saving.
    /// </summary>
    /// <param name="databaseName">
    /// Reads the database base name off the command being validated: a service that refers to the
    /// competitors' database while the session configures none would be started for nobody, which the form
    /// can say now rather than leaving the administrator to notice a missing container at the start of the
    /// competition.
    /// </param>
    public static IRuleBuilderOptions<T, List<SessionDockerImage>> ValidDockerImages<T>(
        this IRuleBuilder<T, List<SessionDockerImage>> rule, Func<T, string?> databaseName) =>
        rule.NotNull().Must(images => images.All(i =>
            !string.IsNullOrWhiteSpace(i.Image)
            && i.PortMappings.All(p => p.HostPort is > 0 and < 65536 && p.ContainerPort is > 0 and < 65536)
            && i.Volumes.All(v => !string.IsNullOrWhiteSpace(v.HostPath) && !string.IsNullOrWhiteSpace(v.ContainerPath))))
            .WithMessage("Each docker image needs a non-empty image name, valid port mappings (1-65535) and non-empty volume paths.")
            // Refused here rather than substituted with an empty value, because an unknown placeholder is
            // always a typo: it reaches the container verbatim, and a connection string carrying
            // "{{database.sever}}" fails as a hostname during the competition instead of as a name on a form.
            .Must(images => UnknownPlaceholders(images).Count == 0)
            .WithMessage((_, images) =>
                $"These docker service settings name placeholders that do not exist: "
                + $"{string.Join(", ", UnknownPlaceholders(images).Select(name => ServiceTemplate.Open + name + ServiceTemplate.Close))}. "
                + $"The ones that do are {string.Join(", ", ServicePlaceholders.All.Select(ServicePlaceholders.TokenOf))}.")
            .Must((command, images) =>
                !images.Any(image => ServiceTemplate.Scan(image).NeedsDatabase)
                || !string.IsNullOrWhiteSpace(databaseName(command)))
            .WithMessage(
                "A docker service that refers to the competitors' database needs the session's database base "
                + "name to be set. Set one, or use only the competitor placeholders.")
            .Must(images => images.All(image => IsUnset(image.Domain) || ServiceDomain.IsValid(image.Domain!)))
            .WithMessage(
                "A docker service's domain must be a bare lowercase hostname such as 'shop.skills.local' — "
                + $"no scheme, no port, no path, at most {ServiceDomain.MaxLength} characters.")
            // Traefik forwards to a port INSIDE the container, so a routed service that publishes nothing
            // over TCP gives it nothing to forward to: the route is created and every request fails.
            .Must(images => images.All(image =>
                IsUnset(image.Domain)
                || image.PortMappings.Any(port => port.Protocol == PortProtocol.Tcp)))
            .WithMessage(
                "A docker service with a domain needs at least one TCP port mapping — the reverse proxy "
                + "forwards to the container port of the first one.")
            // Two services on one hostname would be two proxy routers competing for the same requests, and
            // which one wins is a tie-break nothing here controls.
            .Must(images => Domains(images).Count == Domains(images).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .WithMessage("Two docker services of a session cannot share a domain.");

    private static IReadOnlyList<string> Domains(IEnumerable<SessionDockerImage> images) =>
        [.. images.Select(image => image.Domain).Where(domain => !IsUnset(domain)).Select(domain => domain!)];

    /// <summary>
    /// Every placeholder name the services mention that the catalogue does not hold, each once.
    /// </summary>
    private static IReadOnlyList<string> UnknownPlaceholders(IEnumerable<SessionDockerImage> images) =>
        [.. images.SelectMany(image => ServiceTemplate.Scan(image).Unknown).Distinct(StringComparer.OrdinalIgnoreCase)];
}
