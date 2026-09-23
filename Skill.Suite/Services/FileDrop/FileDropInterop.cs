namespace Skill.Suite.Services.FileDrop;

using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>
/// The files and folders an administrator picks or drops in the browser, read through the helper in
/// <c>js/file-drop.js</c>.
/// </summary>
/// <remarks>
/// Not <c>InputFile</c>, which can do neither half of uploading a folder: it drops the path of every file
/// inside a chosen folder, and cannot receive a dropped folder at all. Both the list of files and each file's
/// bytes come over as <see cref="IJSStreamReference"/> streams rather than as interop values, because Blazor
/// Server refuses a browser message over 32 KB and tears the circuit down when one arrives — the paths of a
/// single folder are easily more than that, while a stream is sent in chunks under the limit.
/// </remarks>
public sealed class FileDropInterop(IJSRuntime js)
{
    /// <summary>Attaches a zone's click, change and drag listeners, and the window-level drop guard.</summary>
    private const string RegisterFunction = "skillSuiteFileDrop.register";

    /// <summary>Detaches them again, and forgets the batches the zone still holds.</summary>
    private const string UnregisterFunction = "skillSuiteFileDrop.unregister";

    /// <summary>A batch's file list, as a JSON Blob.</summary>
    private const string DescribeFunction = "skillSuiteFileDrop.describe";

    /// <summary>One file of a batch: the File itself, which is a Blob.</summary>
    private const string OpenFunction = "skillSuiteFileDrop.open";

    /// <summary>Lets the browser forget a batch.</summary>
    private const string ReleaseFunction = "skillSuiteFileDrop.release";

    /// <summary>
    /// The most a batch's file list may take as JSON: well over a hundred thousand paths, far more than one
    /// upload could hold anyway.
    /// </summary>
    private const long MaxManifestBytes = 16L * 1024 * 1024;

    /// <summary>The buffer <see cref="Stream.CopyToAsync(Stream)"/> uses by default.</summary>
    private const int CopyBufferSize = 81920;

    /// <summary>Names a spool file for what it is, should one ever outlive the process that wrote it.</summary>
    private const string SpoolFilePrefix = "skill-suite-upload-";

    private const string SpoolFileExtension = ".tmp";

    /// <summary>
    /// Makes <paramref name="zone"/> accept dropped files and folders, and a trigger inside it open one of the
    /// two pickers.
    /// </summary>
    /// <remarks>
    /// The pickers have to open in the browser's own click listener. A Blazor Server click travels to the server
    /// and back over SignalR, so by the time an interop call reaches the browser the page is no longer inside
    /// the user gesture, and Safari refuses to open a file dialog there. Whatever is then chosen or dropped is
    /// reported to <c>OnFilesChosen</c> on <paramref name="component"/>, as a batch id and a count.
    /// </remarks>
    public async Task RegisterAsync<T>(
        ElementReference zone,
        ElementReference filesInput,
        ElementReference folderInput,
        DotNetObjectReference<T> component,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await js.InvokeVoidAsync(RegisterFunction, cancellationToken, zone, filesInput, folderInput, component);
        }
        catch (JSDisconnectedException)
        {
            // The circuit dropped before the listeners could be attached. Nothing to attach them to any more —
            // and JSDisconnectedException does not derive from JSException, so it needs its own arm.
        }
        catch (JSException)
        {
            // The script has not loaded. The page still works, only nothing can be picked or dropped, which is
            // a better outcome for the administrator than a failed render.
        }
    }

    /// <summary>Removes what <see cref="RegisterAsync{T}"/> attached, and the batches the zone still holds.</summary>
    /// <remarks>
    /// Called from component disposal, where the circuit is often already gone — that is not a failure worth
    /// reporting, because the browser has thrown the zone away with it.
    /// </remarks>
    public async Task UnregisterAsync(ElementReference zone, CancellationToken cancellationToken)
    {
        try
        {
            await js.InvokeVoidAsync(UnregisterFunction, cancellationToken, zone);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    /// <summary>
    /// Which files a batch holds, numbered in the order <see cref="SpoolAsync"/> knows them by, or
    /// <see langword="null"/> when the browser no longer holds the batch or could not list it.
    /// </summary>
    /// <remarks>
    /// Read as a stream rather than returned from the call, because the list for a folder is easily larger
    /// than the 32 KB one browser message may be. A list larger than <see cref="MaxManifestBytes"/> counts as
    /// one the browser could not supply.
    /// </remarks>
    public async Task<IReadOnlyList<ChosenFile>?> ReadManifestAsync(int batchId, CancellationToken cancellationToken)
    {
        using var manifest = new MemoryStream();
        if (!await CopyBlobAsync(DescribeFunction, [batchId], MaxManifestBytes, manifest, cancellationToken))
            return null;

        // Parsed only once the whole list is here, so a mistake in it surfaces as what it is rather than as a
        // browser that could not read it.
        manifest.Position = 0;
        var entries = await JsonSerializer.DeserializeAsync<List<FileDropManifestEntry>>(
            manifest, JsonSerializerOptions.Web, cancellationToken);

        if (entries is null)
            return null;

        return [.. entries.Select((entry, index) => new ChosenFile(index, entry.Path, entry.Size))];
    }

    /// <summary>
    /// Copies one file of a batch out of the browser into a temporary file on the server, and hands that back
    /// at its start, for the caller to dispose; <see langword="null"/> when the browser could not supply it.
    /// </summary>
    /// <remarks>
    /// Copied first rather than streamed straight into whatever consumes it, so that a failure is put down to
    /// the side it happened on. A browser that can no longer read a file — moved or deleted since it was
    /// chosen — only says so part-way through, as an exception from the remote stream; copying settles that
    /// here, and the consumer then gets an ordinary seekable stream whose failures are its own. That is also
    /// why the catch is narrow and wraps nothing but the browser: it takes the exceptions Blazor raises for a
    /// remote stream that could not be opened, broke off or went quiet, and one larger than
    /// <paramref name="maxAllowedSize"/>, while a failure writing the copy, a cancellation and a circuit that
    /// has gone all propagate. The temporary file deletes itself when the stream is disposed.
    /// <para>
    /// An empty file is never asked for: a stream reference cannot have a length of zero, so the size the
    /// browser reported is taken at its word and an empty stream comes back.
    /// </para>
    /// </remarks>
    public async Task<Stream?> SpoolAsync(
        int batchId,
        ChosenFile file,
        long maxAllowedSize,
        CancellationToken cancellationToken)
    {
        if (file.SizeBytes == 0)
            return new MemoryStream();

        var spool = CreateSpool();
        var handedOver = false;
        try
        {
            if (!await CopyBlobAsync(OpenFunction, [batchId, file.Index], maxAllowedSize, spool, cancellationToken))
                return null;

            spool.Position = 0;
            handedOver = true;
            return spool;
        }
        finally
        {
            if (!handedOver)
                await spool.DisposeAsync();
        }
    }

    /// <summary>Lets the browser forget a batch; whoever received the batch calls this once done with it.</summary>
    /// <remarks>
    /// Swallows what <see cref="UnregisterAsync"/> swallows, for the same reason: releasing is tidying up, and
    /// a circuit that has gone took the batch with it.
    /// </remarks>
    public async Task ReleaseAsync(int batchId, CancellationToken cancellationToken)
    {
        try
        {
            await js.InvokeVoidAsync(ReleaseFunction, cancellationToken, batchId);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    /// <summary>
    /// Asks the script for a Blob and copies all of it into <paramref name="destination"/>; <see langword="false"/>
    /// when the browser could not supply it.
    /// </summary>
    /// <remarks>
    /// A <see cref="JSException"/> is the script refusing — a batch it no longer holds, a file it never had —
    /// and an <see cref="ArgumentOutOfRangeException"/> from opening is a Blob larger than
    /// <paramref name="maxAllowedSize"/>; neither has read anything. The reference is disposed only once the
    /// copy is over, so the browser holds the Blob for as long as it is being streamed.
    /// </remarks>
    private async Task<bool> CopyBlobAsync(
        string function,
        object[] arguments,
        long maxAllowedSize,
        Stream destination,
        CancellationToken cancellationToken)
    {
        IJSStreamReference reference;
        try
        {
            reference = await js.InvokeAsync<IJSStreamReference>(function, cancellationToken, arguments);
        }
        catch (JSException)
        {
            return false;
        }

        await using (reference)
        {
            Stream source;
            try
            {
                source = await reference.OpenReadStreamAsync(maxAllowedSize, cancellationToken);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            await using (source)
            {
                return await CopyFromBrowserAsync(source, destination, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Copies what the browser streams, telling a stream it stopped supplying apart from everything else.
    /// </summary>
    /// <remarks>
    /// Only the reads are guarded: a failure writing the copy is the server's own, and propagates.
    /// </remarks>
    private static async Task<bool> CopyFromBrowserAsync(
        Stream browser,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            while (true)
            {
                int read;
                try
                {
                    read = await browser.ReadAsync(buffer, cancellationToken);
                }
                catch (Exception ex) when (BrokeOff(ex))
                {
                    return false;
                }

                if (read == 0)
                    return true;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// What Blazor's remote stream raises when the browser side broke off: a read the browser failed
    /// (<see cref="InvalidOperationException"/>), chunks that stopped adding up (<see cref="IOException"/>,
    /// <see cref="EndOfStreamException"/> among them) and silence (<see cref="TimeoutException"/>).
    /// </summary>
    private static bool BrokeOff(Exception exception) =>
        exception is InvalidOperationException or IOException or TimeoutException;

    private static FileStream CreateSpool() =>
        new(Path.Combine(Path.GetTempPath(), $"{SpoolFilePrefix}{Guid.NewGuid():N}{SpoolFileExtension}"),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });
}
