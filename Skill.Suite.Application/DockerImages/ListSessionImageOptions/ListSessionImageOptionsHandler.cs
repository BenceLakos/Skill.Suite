namespace Skill.Suite.Application.DockerImages.ListSessionImageOptions;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

/// <summary>
/// Answers with the registered images always, and the registry's images when it can get them.
/// </summary>
/// <remarks>
/// Every failure below degrades to a warning rather than an error. The caller is the session editor, and a
/// registry that is down, mid-restart or refusing the stored credential must not stop an admin editing a
/// session — the image reference can always be typed, and the ones already registered in the database are
/// still perfectly good options.
/// </remarks>
public sealed class ListSessionImageOptionsHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IContainerRegistryClient registry,
    IOptions<WebhookOptions> options,
    ILogger<ListSessionImageOptionsHandler> logger)
    : IRequestHandler<ListSessionImageOptionsQuery, Result<SessionImageOptionsDto>>
{
    /// <summary>
    /// How long the registry may take before the page is rendered without it.
    /// </summary>
    /// <remarks>
    /// The listing walks one request per package owner, so an instance with many accounts on a slow link can
    /// take far longer than anyone will wait for a dropdown. Well above the normal case and well below the
    /// per-request timeout multiplied by the owners it may visit.
    /// </remarks>
    private static readonly TimeSpan DiscoveryBudget = TimeSpan.FromSeconds(15);

    public async ValueTask<Result<SessionImageOptionsDto>> Handle(
        ListSessionImageOptionsQuery request, CancellationToken cancellationToken)
    {
        var preconfigured = await db.DockerImages
            .AsNoTracking()
            .Select(image => image.ImageName)
            .Distinct()
            .OrderBy(image => image)
            .ToListAsync(cancellationToken);

        var (discovered, warning) = await DiscoverAsync(cancellationToken);

        return new SessionImageOptionsDto(ImageOptionMerger.Merge(preconfigured, discovered), warning);
    }

    private async Task<(IReadOnlyList<string> Images, string? Warning)> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        var credential = await db.FindByKindAsync(vault, CredentialKind.Gitea, cancellationToken);
        if (credential is null)
            return ([], RegistryWarnings.MissingCredential);

        var registryHost = RegistryHostResolver.Resolve(options.Value.GitInternalBaseUrl);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(DiscoveryBudget);

        try
        {
            var listing = await registry.ListContainerPackagesAsync(credential, budget.Token);
            if (listing.IsFailure)
                return ([], listing.Error.Message);

            return
            ([
                .. listing.Value
                    .Select(package => RegistryImageReference.Compose(registryHost, package))
                    .OfType<string>(),
            ], null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Listing the registry images exceeded the {Budget}s budget", DiscoveryBudget.TotalSeconds);
            return ([], RegistryWarnings.TimedOut);
        }
    }
}
