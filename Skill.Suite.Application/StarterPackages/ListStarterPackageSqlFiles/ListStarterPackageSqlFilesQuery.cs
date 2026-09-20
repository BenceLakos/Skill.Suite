namespace Skill.Suite.Application.StarterPackages.ListStarterPackageSqlFiles;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Every <c>.sql</c> file on the starter packages volume, wherever in a package it sits.
/// </summary>
/// <remarks>
/// Recursive and across all packages, unlike <c>BrowseStarterPackageQuery</c>, because the administrator
/// picking a seed script is choosing a script rather than walking a tree — the file is as likely to be in
/// <c>seed/</c> or <c>db/</c> as at the top of a package, and making them find it folder by folder is the
/// same list with extra clicks.
/// </remarks>
public sealed record ListStarterPackageSqlFilesQuery : IRequest<Result<IReadOnlyList<StarterPackageEntryDto>>>;
