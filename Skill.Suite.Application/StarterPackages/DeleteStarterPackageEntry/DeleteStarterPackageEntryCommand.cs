namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackageEntry;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>Deletes a file, or a folder with everything in it, inside a package.</summary>
/// <remarks>
/// Never a package itself: that stays <c>DeleteStarterPackageCommand</c>, which the packages list sends by
/// name, so the one delete that takes a whole starter kit with it is never a path that happens to be a
/// single segment.
/// </remarks>
/// <param name="RelativePath">From the starter packages root, at least one level inside a package.</param>
public sealed record DeleteStarterPackageEntryCommand(string RelativePath) : IRequest<Result>;
