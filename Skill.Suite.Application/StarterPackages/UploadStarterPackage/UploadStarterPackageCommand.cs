using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.UploadStarterPackage;

/// <param name="Archive">Read once and left open for the caller to dispose.</param>
/// <param name="Overwrite">Replaces an existing package wholesale rather than merging into it.</param>
public sealed record UploadStarterPackageCommand(string Name, Stream Archive, bool Overwrite) : IRequest<Result>;
