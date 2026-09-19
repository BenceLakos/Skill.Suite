namespace Skill.Suite.Infra.GitHost;

using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

/// <summary>
/// <see cref="IContainerRegistryClient"/> over the Gitea REST API (verified against Gitea 1.26).
/// </summary>
/// <remarks>
/// Gitea has no "list every package on the instance" endpoint: packages are listed per owner, so the owners
/// have to be enumerated first — organisations and users both, because a session judge image is as likely to
/// be published under the administrator's own account as under an organisation.
/// <para>
/// Paging is by a fixed page size with a hard stop, for the same reason the user listing pages: the host caps
/// the page size server-side, so one large <c>limit</c> silently returns a single page and everything past it
/// simply never appears in the dropdown.
/// </para>
/// </remarks>
internal sealed class GiteaContainerRegistryClient(
    HttpClient http, ILogger<GiteaContainerRegistryClient> logger) : IContainerRegistryClient
{
    /// <summary>The only package type the judgement and session images can be.</summary>
    private const string ContainerPackageType = "container";

    /// <summary>Entries per page when listing owners or packages.</summary>
    private const int PageSize = 50;

    /// <summary>Hard stop on paging, so a host that keeps returning full pages cannot spin forever.</summary>
    private const int MaxPages = 40;

    /// <summary>
    /// Hard stop on owners. Each one costs at least one request, and the caller is a dropdown on a page an
    /// admin is waiting for.
    /// </summary>
    private const int MaxOwners = 200;

    private readonly GiteaApi api = new(http);

    public async ValueTask<Result<IReadOnlyList<ContainerPackage>>> ListContainerPackagesAsync(
        BasicCredential credential, CancellationToken cancellationToken)
    {
        try
        {
            var packages = new List<ContainerPackage>();

            foreach (var owner in await ListOwnersAsync(credential, cancellationToken))
                packages.AddRange(await ListPackagesOfAsync(owner, credential, cancellationToken));

            return packages;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not list the container packages on the git host");
            return DockerImageErrors.RegistryUnavailable(ex.Message);
        }
    }

    /// <summary>
    /// Every organisation and user account, deduplicated: a personal organisation and its owner can report
    /// the same name, and asking twice for the same owner's packages would list them twice.
    /// </summary>
    private async Task<IReadOnlyList<string>> ListOwnersAsync(
        BasicCredential credential, CancellationToken cancellationToken)
    {
        var owners = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                owners.Add(name);
        }

        for (var page = 1; page <= MaxPages; page++)
        {
            var batch = await api.GetAsync<List<GiteaOrganization>>(
                $"admin/orgs?limit={PageSize}&page={page}", credential, cancellationToken) ?? [];

            foreach (var organization in batch)
                Add(organization.Name ?? organization.Username);

            if (batch.Count < PageSize)
                break;
        }

        for (var page = 1; page <= MaxPages; page++)
        {
            var batch = await api.GetAsync<List<GiteaUser>>(
                $"admin/users?limit={PageSize}&page={page}", credential, cancellationToken) ?? [];

            foreach (var user in batch)
                Add(user.Login);

            if (batch.Count < PageSize)
                break;
        }

        if (owners.Count <= MaxOwners)
            return owners;

        logger.LogWarning(
            "The git host reports {Count} package owners; only the first {Max} are scanned for images",
            owners.Count, MaxOwners);

        return owners[..MaxOwners];
    }

    /// <summary>
    /// The container packages one owner has published. A 404 means the owner has no package listing at all,
    /// which is not a failure — most accounts never publish one.
    /// </summary>
    private async Task<IReadOnlyList<ContainerPackage>> ListPackagesOfAsync(
        string owner, BasicCredential credential, CancellationToken cancellationToken)
    {
        var packages = new List<ContainerPackage>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var batch = await api.GetAsync<List<GiteaPackage>>(
                $"packages/{GiteaApi.Escape(owner)}?type={ContainerPackageType}&limit={PageSize}&page={page}",
                credential, cancellationToken);

            if (batch is null)
                break;

            packages.AddRange(batch
                .Where(p => string.Equals(p.Type, ContainerPackageType, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(p.Name)
                            && !string.IsNullOrWhiteSpace(p.Version))
                .Select(p => new ContainerPackage(owner, p.Name!, p.Version!)));

            if (batch.Count < PageSize)
                break;
        }

        return packages;
    }
}
