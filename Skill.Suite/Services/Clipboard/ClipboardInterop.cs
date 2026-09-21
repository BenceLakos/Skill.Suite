namespace Skill.Suite.Services.Clipboard;

using Microsoft.JSInterop;

/// <summary>
/// Puts a value on the reader's clipboard, through the browser helper in <c>js/clipboard.js</c>.
/// </summary>
/// <remarks>
/// One place rather than a call per page, because the two call sites drifted once already: both invoked
/// <c>navigator.clipboard.writeText</c> directly, which throws unbound in Chromium and does not exist at all
/// over plain http, so every copy in the application reported failure while looking like it had been written
/// correctly.
/// </remarks>
public sealed class ClipboardInterop(IJSRuntime js)
{
    /// <summary>The browser-side helper, which answers with a boolean instead of throwing.</summary>
    private const string CopyFunction = "skillSuiteClipboard.copy";

    /// <summary>
    /// Whether the value reached the clipboard.
    /// </summary>
    /// <remarks>
    /// Never throws. The helper reports its own failure, and the interop call itself can still fail when the
    /// circuit is being torn down or the script has not loaded — neither of which is worth more to the reader
    /// than "that did not copy, the value is on screen anyway".
    /// </remarks>
    public async Task<bool> CopyAsync(string? value)
    {
        try
        {
            return await js.InvokeAsync<bool>(CopyFunction, value ?? string.Empty);
        }
        catch (JSException)
        {
            return false;
        }
    }
}
