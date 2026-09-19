namespace Skill.Suite.Infra.Containers;

using System.Globalization;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Builds the <c>docker run</c> argument list for a session's long-running service container.
/// </summary>
/// <remarks>
/// Split out from <see cref="ProcessContainerServiceManager"/> so it can be asserted directly, the same way
/// <see cref="DockerRunArguments"/> is: a published port or a read-only flag that silently stops being
/// emitted looks like a working system right up to the moment a competitor needs it.
/// <para>
/// The judgement hardening is deliberately absent here. <c>--cap-drop ALL</c> and <c>--network none</c> exist
/// to contain code a competitor wrote; a service container runs an image an administrator chose, and it is
/// only useful if competitors can reach the ports it publishes — so it stays on the default network, and
/// capabilities are left to the image (a database server dropped to no capabilities does not start). What
/// does carry over is <c>no-new-privileges</c>, which costs a well-behaved image nothing.
/// </para>
/// </remarks>
internal static class DockerServiceArguments
{
    /// <summary>
    /// Keeps the service up for the whole session without resurrecting one an operator stopped on purpose,
    /// and brings it back after a host or daemon restart mid-competition.
    /// </summary>
    private const string RestartPolicy = "unless-stopped";

    private const string ReadOnlySuffix = ":ro";

    /// <summary>Builds the full argument list, in order, excluding the <c>docker</c> executable itself.</summary>
    internal static List<string> Build(ContainerServiceRequest request)
    {
        var args = new List<string>
        {
            "run",
            "-d",
            "--name", request.ContainerName,
            "--restart", RestartPolicy,
            "--security-opt", "no-new-privileges",
        };

        foreach (var port in request.PortMappings)
        {
            args.Add("-p");
            args.Add(Publish(port));
        }

        foreach (var volume in request.Volumes)
        {
            args.Add("-v");
            args.Add(Bind(volume));
        }

        foreach (var variable in request.Environment)
        {
            args.Add("-e");
            args.Add($"{variable.Key}={variable.Value}");
        }

        foreach (var label in request.Labels)
        {
            args.Add("--label");
            args.Add($"{label.Key}={label.Value}");
        }

        // Nothing follows the image: the service image's own entrypoint is the service.
        args.Add(request.Image);

        return args;
    }

    /// <summary>
    /// Renders a port mapping as <c>host:container/protocol</c>, protocol always spelled out.
    /// </summary>
    /// <remarks>
    /// Docker defaults an unsuffixed mapping to tcp, but the domain models the protocol explicitly, so
    /// emitting it keeps a udp service from silently being published as tcp if the suffix logic is ever
    /// trimmed. The name comes from the enum rather than a literal, so a new protocol cannot be added to the
    /// domain without it appearing here.
    /// </remarks>
    private static string Publish(PortMapping port) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{port.HostPort}:{port.ContainerPort}/{port.Protocol.ToString().ToLowerInvariant()}");

    private static string Bind(VolumeMount volume) =>
        $"{volume.HostPath}:{volume.ContainerPath}{(volume.ReadOnly ? ReadOnlySuffix : string.Empty)}";
}
