// Copying text to the clipboard from a Blazor Server page, on a stack served over plain http.
//
// The write has to happen inside the browser's own click handler. A Blazor Server `OnClick` is not that: the
// click is serialised over SignalR, handled on the server, and the JS interop call comes back on a later
// task, by which time the browser no longer considers the page to be acting on a user gesture. Safari refuses
// both `navigator.clipboard.writeText` and `document.execCommand("copy")` outside that gesture, so every copy
// reported failure; Chromium only appeared to work because it keeps transient activation alive for a few
// seconds. No round trip can ever be inside the gesture, so `register` attaches a native click listener and
// copies there, synchronously, before anything is awaited. The result goes back to .NET afterwards, which may
// be async because only the write itself is gesture-bound.
//
// Two further traps, both still avoided here:
//
//   * JS.InvokeVoidAsync("navigator.clipboard.writeText", text) resolves the dotted path and calls the
//     function UNBOUND, so `this` is not the Clipboard and Chromium throws "Illegal invocation".
//   * navigator.clipboard does not exist at all outside a secure context, and a venue LAN is served over
//     plain http — there is no certificate authority to issue for *.skills.local. There the textarea path is
//     the only one there is, and it runs synchronously rather than after a rejected promise, because an
//     awaited rejection has already cost us the gesture.
(function () {
    // The listener attached to each registered element, so unregister can take the same one off again.
    const listeners = new WeakMap();

    function copyWithTextarea(value) {
        const textarea = document.createElement("textarea");
        textarea.value = value;
        textarea.setAttribute("readonly", "");

        // Off-screen rather than hidden: display:none and visibility:hidden are not selectable, and a
        // selection is what execCommand copies. Fixed positioning keeps focusing it from scrolling the page.
        textarea.style.position = "fixed";
        textarea.style.top = "-1000px";
        textarea.style.left = "-1000px";
        textarea.style.opacity = "0";

        document.body.appendChild(textarea);

        try {
            textarea.select();
            textarea.setSelectionRange(0, value.length);
            return document.execCommand("copy");
        } catch {
            return false;
        } finally {
            document.body.removeChild(textarea);
        }
    }

    // Both halves swallow the same failure: the circuit is gone, or the reference was disposed between the
    // click and here, and there is nobody left to show a snackbar to. The call can throw synchronously when
    // the reference itself has been disposed, and it can also reject later when the circuit drops while the
    // message is in flight, so an unchained promise would surface as an unhandled rejection.
    function report(dotNetRef, copied) {
        try {
            dotNetRef.invokeMethodAsync("OnCopied", copied).catch(() => {
                // The circuit is gone. There is nobody left to show a snackbar to.
            });
        } catch {
            // The circuit is gone. There is nobody left to show a snackbar to.
        }
    }

    // Everything before the first `await` still counts as the user gesture, so the clipboard call is made
    // here and only its outcome is handled later.
    function copyFromGesture(value, dotNetRef) {
        if (!window.isSecureContext || !navigator.clipboard) {
            report(dotNetRef, copyWithTextarea(value));
            return;
        }

        navigator.clipboard.writeText(value).then(
            () => report(dotNetRef, true),
            () => report(dotNetRef, copyWithTextarea(value)));
    }

    // The element carries the value as data-copy-value, which Blazor re-renders whenever the parameter
    // changes, so the listener always reads the current one without a second interop call.
    function register(element, dotNetRef) {
        if (!element) {
            return;
        }

        unregister(element);

        const listener = () => copyFromGesture(element.dataset.copyValue ?? "", dotNetRef);

        listeners.set(element, listener);
        element.addEventListener("click", listener);
    }

    function unregister(element) {
        if (!element) {
            return;
        }

        const listener = listeners.get(element);

        if (listener) {
            element.removeEventListener("click", listener);
            listeners.delete(element);
        }
    }

    // Kept for callers that copy something the reader did not click on, such as a value produced by a server
    // action. Those are subject to the gesture rules above and can legitimately fail.
    async function copy(text) {
        const value = text ?? "";

        if (window.isSecureContext && navigator.clipboard) {
            try {
                await navigator.clipboard.writeText(value);
                return true;
            } catch {
                // Refused by a permissions policy, or the document was not focused. The fallback below still
                // works in some of those cases, so this is not the answer yet.
            }
        }

        return copyWithTextarea(value);
    }

    window.skillSuiteClipboard = { copy: copy, register: register, unregister: unregister };
})();
