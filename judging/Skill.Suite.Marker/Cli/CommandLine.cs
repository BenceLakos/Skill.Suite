namespace Skill.Suite.Marker.Cli;

/// <summary>
/// Parses the command line by hand.
/// </summary>
/// <remarks>
/// No argument-parsing library on purpose: the tool is installed into judge images that restore offline,
/// so every dependency is one more package to vendor into <c>local-nuget/</c>.
/// </remarks>
public static class CommandLine
{
    /// <summary>Parses arguments into a command.</summary>
    /// <param name="args">Raw arguments.</param>
    /// <param name="error">Why parsing failed, when it did.</param>
    /// <returns>The command, or <see langword="null"/> on a usage error.</returns>
    public static MarkerCommand? Parse(string[] args, out string? error)
    {
        error = null;

        if (args.Length == 0)
        {
            error = "no subcommand given.";
            return null;
        }

        switch (args[0])
        {
            case "help" or "--help" or "-h":
                return new MarkerCommand(MarkerVerb.Help);
            case "version" or "--version":
                return new MarkerCommand(MarkerVerb.Version);
        }

        var verb = args[0] switch
        {
            "score" => MarkerVerb.Score,
            "report" => MarkerVerb.Report,
            _ => (MarkerVerb?)null,
        };

        if (verb is null)
        {
            error = $"unknown subcommand '{args[0]}'.";
            return null;
        }

        var single = new Dictionary<string, string>(StringComparer.Ordinal);
        var multiple = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var index = 1;
        while (index < args.Length)
        {
            var token = args[index];

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"unexpected argument '{token}'.";
                return null;
            }

            var values = new List<string>();
            index++;
            while (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                values.Add(args[index]);
                index++;
            }

            switch (token)
            {
                // Repeatable, and also multi-valued so an expanded shell glob works:
                //   --trx "$TEST_RESULTS"/*.trx
                case "--trx" or "--coverage":
                    if (values.Count == 0)
                    {
                        error = $"{token} requires at least one value.";
                        return null;
                    }
                    if (!multiple.TryGetValue(token, out var existing))
                        multiple[token] = existing = [];
                    existing.AddRange(values);
                    break;

                case "--map" or "--events" or "--mutation" or "--out":
                    if (values.Count != 1)
                    {
                        error = values.Count == 0
                            ? $"{token} requires a value."
                            : $"{token} takes one value but got {values.Count}.";
                        return null;
                    }
                    single[token] = values[0];
                    break;

                default:
                    error = $"unknown option '{token}'.";
                    return null;
            }
        }

        if (!single.TryGetValue("--map", out var map))
        {
            error = "--map is required.";
            return null;
        }

        if (!single.TryGetValue("--events", out var events))
        {
            error = "--events is required.";
            return null;
        }

        if (verb == MarkerVerb.Report && !single.ContainsKey("--out"))
        {
            error = "--out is required for report.";
            return null;
        }

        if (verb == MarkerVerb.Score && single.ContainsKey("--out"))
        {
            error = "--out is not valid for score.";
            return null;
        }

        if (verb == MarkerVerb.Report &&
            (multiple.ContainsKey("--trx") || multiple.ContainsKey("--coverage") || single.ContainsKey("--mutation")))
        {
            error = "report reads only --events; --trx, --coverage and --mutation are score options.";
            return null;
        }

        return new MarkerCommand(
            verb.Value,
            map,
            events,
            multiple.GetValueOrDefault("--trx"),
            multiple.GetValueOrDefault("--coverage"),
            single.GetValueOrDefault("--mutation"),
            single.GetValueOrDefault("--out", ""));
    }
}
