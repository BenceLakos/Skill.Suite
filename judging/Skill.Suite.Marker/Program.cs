using Skill.Suite.TestLog.Protocol;
using System.Reflection;
using Skill.Suite.Marker.Cli;
using Skill.Suite.Marker.Commands;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Output;

namespace Skill.Suite.Marker;

/// <summary>
/// Entry point: dispatch, and turn every failure into a diagnostic the UI can show.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitProcessingError = 1;
    private const int ExitUsageError = 2;

    private static int Main(string[] args)
    {
        var command = CommandLine.Parse(args, out var error);

        if (command is null)
        {
            Console.Error.WriteLine($"skill-marker: {error}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage.Text);
            return ExitUsageError;
        }

        switch (command.Verb)
        {
            case MarkerVerb.Help:
                Console.Out.WriteLine(Usage.Text);
                return ExitSuccess;

            case MarkerVerb.Version:
                Console.Out.WriteLine(Version());
                return ExitSuccess;
        }

        try
        {
            var map = MarkingMapLoader.Load(command.MapPath);

            switch (command.Verb)
            {
                case MarkerVerb.Score:
                    ScoreCommand.Run(command, map);
                    break;
                case MarkerVerb.Report:
                    ReportCommand.Run(command, map);
                    break;
            }

            return ExitSuccess;
        }
        catch (Exception ex)
        {
            // Always leave a marker-error behind before failing. Without it the run shows only a red status
            // and an exit code, and whoever has to explain the result to a competitor has nothing to go on.
            ReportFatal(command.EventsPath, ex);
            Console.Error.WriteLine($"skill-marker: {ex.Message}");
            return ExitProcessingError;
        }
    }

    /// <summary>
    /// Appends a <c>marker-error</c> event describing the failure. Best-effort: if the events file itself is
    /// the problem, the exit code and stderr are all that remain.
    /// </summary>
    private static void ReportFatal(string eventsPath, Exception ex)
    {
        if (string.IsNullOrWhiteSpace(eventsPath)) return;

        try
        {
            using var sink = new EventSink(eventsPath);
            sink.Write(new MarkerErrorEvent($"skill-marker: {ex.Message}"));
        }
        catch (Exception nested) when (nested is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"skill-marker: could not record the failure in {eventsPath}: {nested.Message}");
        }
    }

    private static string Version() =>
        typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Program).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
