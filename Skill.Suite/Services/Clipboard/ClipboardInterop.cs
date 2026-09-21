namespace Skill.Suite.Services.Clipboard;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>
/// Puts a value on the reader's clipboard, through the browser helper in <c>js/clipboard.js</c>.
/// </summary>
/// <remarks>
/// One place rather than a call per page, because the two call sites drifted once already: both invoked
/// <c>navigator.clipboard.writeText</c> directly, which throws unbound in Chromium and does not exist at all
/// over plain http.
/// </remarks>
public sealed class ClipboardInterop(IJSRuntime js)
{
    /// <summary>Copies a value the reader did not click on, and so is not covered by a user gesture.</summary>
    private const string CopyFunction = "skillSuiteClipboard.copy";

    /// <summary>Attaches the browser-side click listener that does the copying.</summary>
    private const string RegisterFunction = "skillSuiteClipboard.register";

    /// <summary>Detaches that listener again.</summary>
    private const string UnregisterFunction = "skillSuiteClipboard.unregister";

    /// <summary>
    /// Makes clicking <paramref name="element"/> copy its <c>data-copy-value</c> attribute.
    /// </summary>
    /// <remarks>
    /// The copy has to run in the browser's own click handler. A Blazor Server click travels to the server and
    /// back over SignalR, so by the time an interop call reaches the browser the page is no longer inside the
    /// user gesture, and Safari refuses both the clipboard API and <c>execCommand</c> there. The listener
    /// reports the outcome back through <c>OnCopied</c> on <paramref name="component"/>.
    /// </remarks>
    public async Task RegisterAsync<T>(
        ElementReference element,
        DotNetObjectReference<T> component,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            await js.InvokeVoidAsync(RegisterFunction, cancellationToken, element, component);
        }
        catch (JSDisconnectedException)
        {
            // The circuit dropped before the listener could be attached. Nothing to attach it to any more —
            // and JSDisconnectedException does not derive from JSException, so it needs its own arm.
        }
        catch (JSException)
        {
            // The script has not loaded. The value is still on screen and selectable by hand, which is a
            // better outcome for the reader than a failed render.
        }
    }

    /// <summary>Removes the listener <see cref="RegisterAsync{T}"/> attached.</summary>
    /// <remarks>
    /// Called from component disposal, where the circuit is often already gone — that is not a failure worth
    /// reporting, because the browser has thrown the element away with it.
    /// </remarks>
    public async Task UnregisterAsync(ElementReference element, CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync(UnregisterFunction, cancellationToken, element);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    /// <summary>
    /// Whether the value reached the clipboard.
    /// </summary>
    /// <remarks>
    /// Never throws. The helper reports its own failure, and the interop call itself can still fail when the
    /// circuit is being torn down or the script has not loaded — neither of which is worth more to the reader
    /// than "that did not copy, the value is on screen anyway". Because this goes through the server it is
    /// outside the user gesture, so it is for values the reader did not click on; a click-driven copy belongs
    /// on <see cref="RegisterAsync{T}"/>.
    /// </remarks>
    public async Task<bool> CopyAsync(string? value, CancellationToken cancellationToken = default)
    {
        try
        {
            return await js.InvokeAsync<bool>(CopyFunction, cancellationToken, value ?? string.Empty);
        }
        catch (JSException)
        {
            return false;
        }
    }
}
