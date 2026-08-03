namespace Skill.Suite.Services.Translation;

public interface ITranslator
{
    string this[string key] { get; }
    string Format(string key, params object[] args);
}
