namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Xunit;

/// <summary>
/// The exact labels that make the reverse proxy send each competitor to their own container.
/// </summary>
/// <remarks>
/// Asserted literally, key by key, because nothing else catches a mistake here. Traefik does not reject a
/// label it cannot use and does not reject a rule that matches nothing: a misspelled key is silently ignored
/// and a wrong address silently matches no request, so a broken route looks exactly like a healthy container
/// until a competitor opens the URL and gets somebody else's work — or a 404.
/// </remarks>
public sealed class TraefikLabelsTests
{
    private const string Container = "skill-suite-round-1-1-c01";
    private const string Network = "skill-suite";

    private static TraefikRoute Route(string? clientIp = "10.0.0.7", int containerPort = 80) =>
        new(Container, "shop.skills.local", clientIp, containerPort, Network);

    [Fact]
    public void TheProxyIsTurnedOnForThisContainerOnly()
    {
        // The proxy runs with exposedbydefault=false, so without this the container is simply not routed.
        Assert.Equal("true", TraefikLabels.For(Route())["traefik.enable"]);
    }

    [Fact]
    public void TheNetworkTheProxyReachesTheContainerOnIsNamed()
    {
        // A container can be on several networks; without this Traefik may pick the wrong address and every
        // request times out.
        Assert.Equal(Network, TraefikLabels.For(Route())["traefik.docker.network"]);
    }

    [Fact]
    public void TheRouterIsBoundToTheHttpEntrypointByName()
    {
        // Not left to the default of "every entrypoint": the stack also has a raw TCP entrypoint for SQL
        // Server, and a router bound to it as well would try to answer TDS with HTTP.
        Assert.Equal(
            "web",
            TraefikLabels.For(Route())[$"traefik.http.routers.{Container}.entrypoints"]);
    }

    [Fact]
    public void TheRouterPointsAtAServiceOfTheSameName()
    {
        var labels = TraefikLabels.For(Route());

        Assert.Equal(Container, labels[$"traefik.http.routers.{Container}.service"]);
        Assert.True(labels.ContainsKey($"traefik.http.services.{Container}.loadbalancer.server.port"));
    }

    [Fact]
    public void TheForwardedPortIsTheContainerPort()
    {
        // The container port, not the published host port: the proxy reaches the container over the shared
        // docker network, where nothing is published.
        Assert.Equal(
            "8081",
            TraefikLabels.For(Route(containerPort: 8081))[
                $"traefik.http.services.{Container}.loadbalancer.server.port"]);
    }

    [Fact]
    public void TheRuleMatchesTheHostAndTheSourceAddress()
    {
        Assert.Equal(
            "Host(`shop.skills.local`) && ClientIP(`10.0.0.7`)",
            TraefikLabels.For(Route())[$"traefik.http.routers.{Container}.rule"]);
    }

    [Fact]
    public void ARouteWithNoSourceAddressMatchesTheHostAlone()
    {
        // Not produced by the planner — every container belongs to one competitor and therefore to one
        // address — but the builder must not invent an empty ClientIP matcher, which would match nothing.
        Assert.Equal(
            "Host(`shop.skills.local`)",
            TraefikLabels.For(Route(clientIp: null))[$"traefik.http.routers.{Container}.rule"]);
    }

    [Fact]
    public void TheRouterNameComesFromTheContainerName()
    {
        // Already unique on the daemon, and already carrying the session, the service position and the
        // competitor — anything else would need a second uniqueness argument.
        Assert.Equal(Container, TraefikLabels.NameOf(Container));
    }

    [Theory]
    [InlineData("skill-suite-round-1-1-joe.doe", "skill-suite-round-1-1-joe-doe")]
    [InlineData("skill-suite-round-1-1-joe_doe", "skill-suite-round-1-1-joe-doe")]
    [InlineData("skill-suite-round-1-1-C01", "skill-suite-round-1-1-c01")]
    public void ACharacterARouterNameMayNotCarryIsReplaced(string containerName, string expected)
    {
        // A username may legitimately contain a dot or an underscore, and a dot in a router name would be
        // read as another level of Traefik's own label tree.
        Assert.Equal(expected, TraefikLabels.NameOf(containerName));
    }

    [Fact]
    public void TwoContainersNeverShareARouterName()
    {
        Assert.NotEqual(
            TraefikLabels.NameOf("skill-suite-round-1-1-c01"),
            TraefikLabels.NameOf("skill-suite-round-1-1-c02"));
    }

    [Fact]
    public void ARouteProducesExactlySixLabels()
    {
        // Pinned so a seventh cannot be added without a deliberate decision: every label here ends up on
        // every routed container of every session.
        Assert.Equal(6, TraefikLabels.For(Route()).Count);
    }
}
