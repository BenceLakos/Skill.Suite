namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Sessions;
using Xunit;

/// <summary>
/// The arithmetic that keeps twenty copies of one service off each other's host ports.
/// </summary>
/// <remarks>
/// A fixed host port is the reason a session's services used to be one container each. Reading it as a base
/// and adding the competitor's ordinal is what makes N copies possible at all, so the sum, the ceiling and
/// the fact that the container port is left alone are all worth pinning: a container port shifted with the
/// host port would publish a port nothing inside the image is listening on.
/// </remarks>
public sealed class ServicePortAllocationTests
{
    private static readonly IReadOnlyList<PortMapping> Configured =
    [
        new PortMapping(5432, 5432, PortProtocol.Tcp),
        new PortMapping(5300, 53, PortProtocol.Udp),
    ];

    [Fact]
    public void TheFirstCompetitorGetsExactlyThePortTheAdministratorTyped()
    {
        var allocated = ServicePortAllocation.For(Configured, Session.FirstOrdinal);

        Assert.True(allocated.IsSuccess);
        Assert.Equal(5432, allocated.Value[0].HostPort);
        Assert.Equal(5300, allocated.Value[1].HostPort);
    }

    [Fact]
    public void EveryMappingIsShiftedByTheSameOrdinal()
    {
        var allocated = ServicePortAllocation.For(Configured, 3);

        Assert.True(allocated.IsSuccess);
        Assert.Equal(5435, allocated.Value[0].HostPort);
        Assert.Equal(5303, allocated.Value[1].HostPort);
    }

    [Fact]
    public void TheContainerPortAndTheProtocolAreLeftAlone()
    {
        // Only the host side moves: the port inside the container is what the image listens on, and shifting
        // it would publish a port nothing answers.
        var allocated = ServicePortAllocation.For(Configured, 7);

        Assert.True(allocated.IsSuccess);
        Assert.Equal(5432, allocated.Value[0].ContainerPort);
        Assert.Equal(53, allocated.Value[1].ContainerPort);
        Assert.Equal(PortProtocol.Udp, allocated.Value[1].Protocol);
    }

    [Fact]
    public void TwoCompetitorsNeverShareAHostPort()
    {
        var first = ServicePortAllocation.For(Configured, 0).Value;
        var second = ServicePortAllocation.For(Configured, 1).Value;

        Assert.NotEqual(first[0].HostPort, second[0].HostPort);
    }

    [Fact]
    public void TheSameOrdinalAlwaysGivesTheSamePorts()
    {
        // Restarting the session has to hand a competitor the port they have already written down.
        Assert.Equal(
            ServicePortAllocation.For(Configured, 4).Value[0].HostPort,
            ServicePortAllocation.For(Configured, 4).Value[0].HostPort);
    }

    [Fact]
    public void ThePortAtTheVeryTopOfTheRangeIsStillAllowed()
    {
        var allocated = ServicePortAllocation.For(
            [new PortMapping(ServicePortAllocation.MaxPort - 1, 80, PortProtocol.Tcp)], 1);

        Assert.True(allocated.IsSuccess);
        Assert.Equal(ServicePortAllocation.MaxPort, allocated.Value[0].HostPort);
    }

    [Fact]
    public void APortPastTheTopOfTheRangeIsThatCompetitorsFailure()
    {
        // Reported rather than wrapped or clamped: a wrapped port would publish on something else entirely,
        // and a clamped one would collide with whoever already has 65535.
        var allocated = ServicePortAllocation.For(
            [new PortMapping(ServicePortAllocation.MaxPort, 80, PortProtocol.Tcp)], 1);

        Assert.True(allocated.IsFailure);
        Assert.Equal("Session.ServicePortOutOfRange", allocated.Error.Code);
    }

    [Fact]
    public void OneOverflowingMappingFailsTheWholeSetForThatCompetitor()
    {
        // Half a service is not a service: a container published on one of its two ports would look like it
        // started and fail the moment the second one was needed.
        var allocated = ServicePortAllocation.For(
            [
                new PortMapping(8080, 80, PortProtocol.Tcp),
                new PortMapping(ServicePortAllocation.MaxPort, 443, PortProtocol.Tcp),
            ],
            1);

        Assert.True(allocated.IsFailure);
    }

    [Fact]
    public void AServiceWithNoPortsAllocatesNothingAndSucceeds()
    {
        var allocated = ServicePortAllocation.For([], 12);

        Assert.True(allocated.IsSuccess);
        Assert.Empty(allocated.Value);
    }
}
