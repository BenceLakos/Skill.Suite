using System.Globalization;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Containers;

/// <summary>
/// Builds the <c>docker run</c> argument list for a judgement container.
/// </summary>
/// <remarks>
/// Split out from <see cref="ProcessContainerRunner"/> so it can be asserted directly. The flags here decide
/// whether a competitor's test code can reach the network and how much of the host it can consume, and a
/// silently dropped flag looks exactly like a working system until someone abuses it.
/// </remarks>
internal static class DockerRunArguments
{
    /// <summary>Builds the full argument list, in order, excluding the <c>docker</c> executable itself.</summary>
    internal static List<string> Build(ContainerRunRequest request)
    {
        // --mount with volume-subpath (Docker 25+) lets the daemon mount only the
        // submission's folder inside the shared workdir volume, so judgement containers
        // can't see sibling submissions even though they share the same backing volume.
        var sourceMount =
            $"type=volume,source={request.WorkdirVolumeName}," +
            $"destination={request.ContainerWorkdir}," +
            $"volume-subpath={request.WorkdirSubpath},readonly";

        var args = new List<string>
        {
            "run",
            "--rm",
            "--name", request.ContainerName,
            "--mount", sourceMount,
            "-w", request.ContainerWorkdir,
        };

        // Optional writable mount for the JSON-lines event log. We don't pass `readonly`
        // here — the judgement image needs to write the log file at this path.
        if (request.LogMount is not null)
        {
            var logMount =
                $"type=volume,source={request.LogMount.VolumeName}," +
                $"destination={request.LogMount.ContainerPath}," +
                $"volume-subpath={request.LogMount.Subpath}";
            args.Add("--mount");
            args.Add(logMount);
        }

        // Isolation and resource caps. The image pull happens before the container's namespaces are created,
        // so --network none does not interfere with pulling from a private registry.
        if (request.Limits is { } limits)
        {
            if (limits.IsolateNetwork)
            {
                args.Add("--network");
                args.Add("none");
            }

            if (!string.IsNullOrWhiteSpace(limits.Memory))
            {
                args.Add("--memory");
                args.Add(limits.Memory);
            }

            if (!string.IsNullOrWhiteSpace(limits.Cpus))
            {
                args.Add("--cpus");
                args.Add(limits.Cpus);
            }

            if (limits.PidsLimit is { } pids)
            {
                args.Add("--pids-limit");
                args.Add(pids.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (request.Environment is not null)
        {
            foreach (var kv in request.Environment)
            {
                args.Add("-e");
                args.Add($"{kv.Key}={kv.Value}");
            }
        }

        // No command or arguments after the image: the judgement image's own ENTRYPOINT is the judge.
        args.Add(request.Image);

        return args;
    }
}
