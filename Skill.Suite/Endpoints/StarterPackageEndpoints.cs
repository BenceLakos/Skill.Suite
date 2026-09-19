using Mediator;
using Microsoft.AspNetCore.Authorization;
using Skill.Suite.Application.Authorization;
using Skill.Suite.Application.StarterPackages.DownloadStarterPackage;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Endpoints;

public static class StarterPackageEndpoints
{
    public static IEndpointRouteBuilder MapStarterPackageEndpoints(this IEndpointRouteBuilder app)
    {
        // A download leaves the Blazor circuit for the browser's own request, so the route carries its own
        // authorization rather than inheriting the page's. Administrators only: a starter package is the
        // session's answer key neighbourhood, and competitors receive it through their repository instead.
        var group = app.MapGroup("/api/starter-packages")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Admin });

        group.MapGet("/download", async (
            string? path,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DownloadStarterPackageQuery(path ?? string.Empty), cancellationToken);
            if (result.IsFailure)
                return MapError(result.Error);

            var download = result.Value;
            return Results.Stream(
                output => download.WriteToAsync(output, cancellationToken),
                download.ContentType,
                download.FileName);
        });

        return app;
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(error),
        ErrorType.Validation => Results.BadRequest(error),
        ErrorType.Conflict => Results.Conflict(error),
        _ => Results.Problem(detail: error.Message, statusCode: 500, title: error.Code),
    };
}
