using Microsoft.JSInterop;

namespace Skill.Suite.Services.Theme;

public sealed class ThemePersistence(IJSRuntime js)
{
    private const string StorageKey = "skillsuite-theme";

    public async ValueTask<bool?> ReadAsync()
    {
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return value switch
            {
                "dark" => true,
                "light" => false,
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async ValueTask WriteAsync(bool isDark)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, isDark ? "dark" : "light");
        }
        catch (Exception)
        {
            // best effort: prerender or JS not ready
        }
    }
}
