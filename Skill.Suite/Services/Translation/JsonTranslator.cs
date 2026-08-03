namespace Skill.Suite.Services.Translation;

public sealed class JsonTranslator(TranslationStore store) : ITranslator
{
    private string _culture = TranslationStore.DefaultCulture;

    public string Culture
    {
        get => _culture;
        set => _culture = string.IsNullOrWhiteSpace(value) ? TranslationStore.DefaultCulture : value;
    }

    public string this[string key] => store.Get(_culture, key);

    public string Format(string key, params object[] args) =>
        string.Format(store.Get(_culture, key), args);
}
