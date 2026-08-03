using System.Text.Json;

namespace Skill.Suite.Services.Translation;

public sealed class TranslationStore
{
    public const string DefaultCulture = "en";

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _byCulture;

    public TranslationStore(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Resources", "Translations");
        var loaded = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(path))
        {
            foreach (var file in Directory.GetFiles(path, "*.json"))
            {
                var culture = Path.GetFileNameWithoutExtension(file);
                var content = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(content)
                           ?? new Dictionary<string, string>();
                loaded[culture] = dict;
            }
        }

        _byCulture = loaded;
    }

    public string Get(string culture, string key)
    {
        if (_byCulture.TryGetValue(culture, out var dict) && dict.TryGetValue(key, out var value))
            return value;

        if (!culture.Equals(DefaultCulture, StringComparison.OrdinalIgnoreCase)
            && _byCulture.TryGetValue(DefaultCulture, out var fallback)
            && fallback.TryGetValue(key, out var fallbackValue))
            return fallbackValue;

        return key;
    }
}
