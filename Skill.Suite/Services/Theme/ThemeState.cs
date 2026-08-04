namespace Skill.Suite.Services.Theme;

public sealed class ThemeState
{
    private bool _isDarkMode;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode == value) return;
            _isDarkMode = value;
            OnChange?.Invoke();
        }
    }

    public event Action? OnChange;

    public void Toggle() => IsDarkMode = !IsDarkMode;
}
