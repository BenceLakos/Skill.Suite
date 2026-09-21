// Copying text to the clipboard from a Blazor circuit, on a stack served over plain http.
//
// Two separate reasons the obvious interop call never worked here, and both of them threw, which is why every
// copy reported failure:
//
//   * JS.InvokeVoidAsync("navigator.clipboard.writeText", text) resolves the dotted path and calls the
//     function UNBOUND, so `this` is not the Clipboard and Chromium throws "Illegal invocation".
//   * navigator.clipboard does not exist at all outside a secure context, and a venue LAN is served over
//     plain http — there is no certificate authority to issue for *.skills.local.
//
// So: the real API when it is actually available, and the old execCommand path otherwise. Nothing here uses
// `this`, so it does not matter how the interop layer invokes it, and nothing throws — the caller gets a
// boolean and decides what to tell the reader.
(function () {
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

    async function copy(text) {
        const value = text ?? "";

        if (window.isSecureContext && navigator.clipboard) {
            try {
                await navigator.clipboard.writeText(value);
                return true;
            } catch {
                // Refused by a permissions policy, or the document was not focused. The fallback below still
                // works in both cases, so this is not the answer yet.
            }
        }

        return copyWithTextarea(value);
    }

    window.skillSuiteClipboard = { copy: copy };
})();
