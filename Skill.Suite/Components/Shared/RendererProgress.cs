namespace Skill.Suite.Components.Shared;

/// <summary>
/// An <see cref="IProgress{T}"/> for Blazor components: every report is applied on the renderer's
/// synchronisation context, and reports arriving after the component is gone are dropped.
/// </summary>
/// <remarks>
/// <see cref="System.Progress{T}"/> is not usable here. It captures the synchronisation context of whoever
/// constructed it, and a Blazor event handler may already be off the renderer's context by then — so the
/// callback can run on a thread pool thread, mutate component state and call StateHasChanged from outside
/// the dispatcher, which is exactly the race that tears a circuit down. Handing the component's own
/// <c>InvokeAsync</c> in makes the marshalling explicit instead of ambient.
/// <para>
/// Dispose it when the operation ends, and again when the owning component is disposed: a long-running
/// command keeps reporting until it notices, and applying a report to a disposed component throws on the
/// dispatcher rather than at the sender.
/// </para>
/// </remarks>
/// <param name="dispatch">The component's <c>InvokeAsync</c>.</param>
/// <param name="apply">What to do with a report, run on the renderer: usually assign and re-render.</param>
public sealed class RendererProgress<T>(Func<Action, Task> dispatch, Action<T> apply) : IProgress<T>, IDisposable
{
    private volatile bool _disposed;

    public void Report(T value)
    {
        if (_disposed)
            return;

        // Fire and forget by contract: IProgress.Report returns immediately, and the dispatcher keeps the
        // reports in the order they were made. The task is still awaited inside ApplyAsync so a fault is
        // observed there rather than resurfacing as an unobserved exception at collection time.
        _ = ApplyAsync(value);
    }

    private async Task ApplyAsync(T value)
    {
        try
        {
            await dispatch(() =>
            {
                if (!_disposed)
                    apply(value);
            });
        }
        catch
        {
            // Nothing downstream can act on a progress update that could not be drawn, and the operation
            // being reported on must not be disturbed by it. The circuit going away mid-report is the
            // expected case: the admin navigated off the page while provisioning was still running.
        }
    }

    public void Dispose() => _disposed = true;
}
