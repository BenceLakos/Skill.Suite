namespace Skill.Suite.Application.StarterPackages.PlanStarterPackageUpload;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// What uploading these files into a folder would do, asked before any of them is sent.
/// </summary>
/// <remarks>
/// A question of its own rather than part of the upload, because the browser sends each file as a separate
/// command afterwards: the administrator has to be able to confirm the replacements, and be told a batch is
/// too large, before the first byte moves rather than halfway through.
/// </remarks>
/// <param name="FolderPath">
/// From the starter packages root, e.g. <c>fibonacci-session/competitor-start</c>; a package itself, such as
/// <c>fibonacci-session</c>, is a folder too.
/// </param>
public sealed record PlanStarterPackageUploadQuery(
    string FolderPath,
    IReadOnlyList<StarterPackageUploadFileDto> Files)
    : IRequest<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>>;
