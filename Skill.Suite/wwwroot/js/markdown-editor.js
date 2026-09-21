window.skillSuite = window.skillSuite || {};

window.skillSuite.markdown = {
    init(textarea, dotnetRef, initialValue) {
        const editor = new EasyMDE({
            element: textarea,
            initialValue: initialValue || "",
            spellChecker: false,
            autoDownloadFontAwesome: false,
            status: false,
            minHeight: "260px",
            toolbar: [
                "bold", "italic", "heading", "|",
                "quote", "unordered-list", "ordered-list", "|",
                "link", "code", "table", "|",
                "preview", "side-by-side", "fullscreen", "|",
                "guide"
            ],
        });

        let last = initialValue || "";
        editor.codemirror.on("change", () => {
            const value = editor.value();
            if (value === last) return;
            last = value;
            dotnetRef.invokeMethodAsync("OnContentChanged", value);
        });

        return {
            setValue(value) {
                if (editor.value() === value) return;
                last = value || "";
                editor.value(value || "");
            },
            destroy() {
                editor.toTextArea();
            },
        };
    }
};
