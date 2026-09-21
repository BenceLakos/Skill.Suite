namespace Skill.Suite.Configuration;

using Microsoft.AspNetCore.HttpOverrides;

/// <summary>
/// Takes the caller's address from <c>X-Forwarded-For</c>, from whichever peer sent it.
/// </summary>
/// <remarks>
/// The deployment puts Traefik in front of the application, so without this every request arrives from the
/// proxy's own address on the docker network and the sign-in page's workstation pre-fill sees one address for
/// the whole venue.
/// <para>
/// No peer is checked, which is a deliberate trade-off rather than an oversight: leaving
/// <see cref="ForwardedHeadersOptions.KnownProxies"/> and
/// <see cref="ForwardedHeadersOptions.KnownIPNetworks"/> empty is how this middleware is told to trust any
/// proxy, and it means anyone who can reach the port can claim to be sitting at any workstation — which for
/// the pre-fill is the same as asking for that competitor's password. The stack runs on a private venue
/// network where that reach is the competition's own, and the cost of the alternative was a subnet that has
/// to be re-read out of docker after every recreation of the network.
/// </para>
/// <para>
/// Only <c>X-Forwarded-For</c> is read, and only its right-most entry. Traefik APPENDS the peer it saw to
/// whatever the client sent, so a limit of one consumes the proxy's own observation and a value the client
/// made up stays to the left of it, unread.
/// </para>
/// </remarks>
internal static class ForwardedHeadersSetup
{
    /// <summary>How many <c>X-Forwarded-For</c> entries are read, counting from the right.</summary>
    private const int TrustedProxyHops = 1;

    public static IServiceCollection AddProxyForwarding(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = TrustedProxyHops;

            // Empty on purpose, and cleared rather than left at the defaults, which trust loopback only: with
            // neither list populated the middleware skips the known-address check altogether, which is the
            // documented way to accept the header from any proxy.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
        });
}
