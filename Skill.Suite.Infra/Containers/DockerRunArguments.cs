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

            // Applied to every run, not made optional. A judgement container executes code written by someone
            // with an incentive to score higher, so the only defensible default is the restrictive one.
            //
            // --cap-drop=ALL: nothing the judge pipeline does needs a Linux capability. Restoring, building and
            // running tests are ordinary file and process operations. Dropping them removes whole classes of
            // container escape from reach without costing the legitimate workload anything.
            //
            // --security-opt=no-new-privileges: stops a setuid binary inside the image from raising privileges,
            // which is what makes the capability drop stick rather than being a speed bump.
            "--cap-drop", "ALL",

            // Added back for the privilege drop, and nothing wider. The judge pipeline drops to an
            // unprivileged account for the test step, which needs SETUID/SETGID to change identity and
            // CHOWN/DAC_OVERRIDE/FOWNER to hand that account its build output and results directory.
            // Dropping ALL without these made setpriv fail with "setresuid: Operation not permitted" — the
            // two hardening measures cancelled each other out, and the run produced no events at all.
            //
            // The competitor's own code never holds these: it executes after the drop, as an unprivileged user,
            // and no-new-privileges stops it climbing back.
            "--cap-add", "SETUID",
            "--cap-add", "SETGID",
            "--cap-add", "CHOWN",
            "--cap-add", "DAC_OVERRIDE",
            "--cap-add", "FOWNER",

            // KILL is what makes the judge's wall clock real. The `timeout` in judge-lib.sh runs as root, but
            // the step it guards has already been handed to an unprivileged uid by setpriv, and signalling a
            // process that does not share your uid needs CAP_KILL. Without it the SIGTERM and the follow-up
            // SIGKILL both come back EPERM, `timeout` succeeds only in killing itself, and the test host runs
            // on until the container is torn down: a submission with an endless loop was recorded as
            // Completed, carrying whatever partial results it had reached.
            //
            // Adding it back does not soften the drop. It is held only by the judge's own root processes —
            // the competitor's code runs after the privilege drop, under no-new-privileges, so it can never
            // acquire it — and CAP_KILL grants nothing but the right to signal processes inside this
            // container's own pid namespace. There is no host process and no sibling container within reach.
            "--cap-add", "KILL",

            "--security-opt", "no-new-privileges",
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
