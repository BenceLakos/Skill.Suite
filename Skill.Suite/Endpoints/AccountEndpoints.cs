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

            return Results.Redirect(LocalReturnUrl(returnUrl));
        }).DisableAntiforgery();

        group.MapPost("/Logout", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new LogoutCommand(), ct);
            return Results.Redirect("/Account/Login");
        }).DisableAntiforgery();

        return app;
    }

    /// <summary>
    /// Reduces a submitted <c>returnUrl</c> to a path on this host, or "/" when it is anything else.
    /// </summary>
    /// <remarks>
    /// <c>Uri.IsWellFormedUriString(…, UriKind.Relative)</c> is not this check and was the bug: it accepts a
    /// protocol-relative URL, so <c>returnUrl=//evil.example/login</c> was treated as a local path and the
    /// browser resolved the redirect to another host. That turns a genuine successful login on the real site
    /// into a hand-off to a phishing page, which is exactly what a post-login redirect must not be able to do.
    /// A single leading slash, and no scheme or authority, is the whole requirement.
    /// </remarks>
    private static string LocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        // A backslash is folded to a forward slash by browsers, so `/\evil.example` is protocol-relative too.
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
        && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/";
}
