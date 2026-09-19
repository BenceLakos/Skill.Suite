using System.Globalization;

namespace Skill.Suite.Components.Shared;

/// <summary>Renders a byte count the way an operator reads it: "1.4 MB", not "1468006".</summary>
public static class ByteSizeFormatter
{
    private const long Scale = 1024;
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        if (bytes < Scale)
            return $"{bytes} {Units[0]}";

        double value = bytes;
        var unit = 0;
        while (value >= Scale && unit < Units.Length - 1)
        {
            value /= Scale;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
    }
}
