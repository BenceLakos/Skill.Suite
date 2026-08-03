using Mediator;
using Microsoft.AspNetCore.Mvc;
using Skill.Suite.Application.Auth.Login;
using Skill.Suite.Application.Auth.Logout;

namespace Skill.Suite.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Account").AllowAnonymous();

        group.MapPost("/Login", async (
            [FromForm(Name = "Username")] string username,
            [FromForm(Name = "Password")] string password,
            [FromForm(Name = "RememberMe")] bool? rememberMe,
            [FromForm(Name = "ReturnUrl")] string? returnUrl,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(username, password, rememberMe ?? false), ct);

            if (result.IsFailure)
                return Results.Redirect($"/Account/Login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");

            var target = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                ? returnUrl
                : "/";

            return Results.Redirect(target);
        }).DisableAntiforgery();

        group.MapPost("/Logout", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new LogoutCommand(), ct);
            return Results.Redirect("/Account/Login");
        }).DisableAntiforgery();

        return app;
    }
}
