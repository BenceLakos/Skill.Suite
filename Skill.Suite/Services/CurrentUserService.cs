using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Services;

public class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authStateProvider) : ICurrentUserService
{
    private ClaimsPrincipal? ResolveUser()
    {
        // HTTP request scopes (initial Razor server render, REST endpoints,
        // anonymous webhooks) all expose HttpContext — even if the principal is
        // anonymous, HttpContext.User is the authoritative answer.
        var http = httpContextAccessor.HttpContext;
        if (http is not null)
            return http.User;

        // No HTTP request: we're either inside a Blazor Server SignalR circuit
        // (where AuthenticationStateProvider is the circuit-scoped source) or a
        // background-service scope (where it throws because there is no Razor
        // component DI scope). The catch covers the latter — no user available.
        try
        {
            var task = authStateProvider.GetAuthenticationStateAsync();
            var state = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();
            return state.User;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private ClaimsPrincipal? User => ResolveUser();

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
