// Handing the files and folders an administrator picks or drops to a Blazor Server page, with the paths
// inside a folder kept, so a folder can be uploaded as the folder it is.
//
// Blazor's own InputFile can do neither half of that. It drops webkitRelativePath — blazor.web.js maps every
// File to {id, lastModified, name, size, contentType, blob} and nothing more — so a chosen folder arrives as
// a flat list of names, and it has no way to receive a directory dropped from the desktop at all. So this
// collects the files itself, from two hidden inputs (one of them a folder picker) and from drops, whose
// folders it walks with the entry API, and hands their bytes over exactly the way InputFile does inside:
// `open` returns the File itself, which is a Blob, and Blazor turns a Blob into a stream when .NET asks for
// an IJSStreamReference.
//
// Nothing describing the files travels as an interop argument or return value. Blazor Server's hub refuses a
// client-to-server message over 32 KB (MaximumReceiveMessageSize) and tears the circuit down when one
// arrives, and the paths of a folder of a few hundred files are already more than that. So .NET is only told
// a batch id and a count, and reads the list itself through `describe`, as a JSON Blob that is streamed in
// chunks under that limit like any file.
//
// The pickers open from the zone's own click listener, because a file dialog only opens inside the user
// gesture — the same rule clipboard.js explains: a Blazor OnClick reaches the browser again after a round
// trip, outside it, and Safari then refuses. So a trigger inside the zone carries data-file-drop-pick="files"
// or "folder" instead of an OnClick, and the listener calls input.click() synchronously.
//
// While any zone is registered, a window-level guard refuses file drops everywhere else on the page: a file
// dropped beside the zone would make the browser open it in the page's place, which ends the circuit and
// every upload running in it.
(function () {
    const pickAttribute = "data-file-drop-pick";
    const pickSelector = `[${pickAttribute}]`;
    const pickFolder = "folder";
    const activeClass = "file-drop-active";
    const disabledState = "true";
    const filesType = "Files";
    const fileKind = "file";
    const copyEffect = "copy";
    const refusedEffect = "none";
    const manifestType = "application/json";
    const chosenCallback = "OnFilesChosen";
    const leadingSeparators = /^\/+/;
    const logPrefix = "skillSuiteFileDrop:";

    // What register attached to each zone, so unregister can take exactly that off again.
    const registrations = new Map();

    // The files each pick or drop handed over, by batch id, until .NET releases them: it asks for them one at
    // a time, long after the event that chose them is over.
    const batches = new Map();
    let nextBatchId = 0;
    let guarding = false;

    function isDisabled(zone) {
        return zone.dataset.disabled === disabledState;
    }

    // Only a drag that carries files is this script's business. Text or a link dragged within the page keeps
    // the browser's own behaviour.
    function carriesFiles(event) {
        const types = event.dataTransfer ? event.dataTransfer.types : null;
        return !!types && Array.prototype.includes.call(types, filesType);
    }

    function showOverlay(registration, visible) {
        registration.zone.classList.toggle(activeClass, visible);
    }

    // Everything before input.click() still counts as the user gesture, so nothing here may wait.
    function pick(registration, event) {
        const target = event.target;

        // The click input.click() dispatches on the input bubbles back up through the zone.
        if (target === registration.filesInput || target === registration.folderInput) {
            return;
        }

        const trigger = target instanceof Element ? target.closest(pickSelector) : null;
        if (!trigger || !registration.zone.contains(trigger) || trigger.matches(":disabled") ||
            isDisabled(registration.zone)) {
            return;
        }

        const input = trigger.getAttribute(pickAttribute) === pickFolder
            ? registration.folderInput
            : registration.filesInput;

        input.click();
    }

    function choose(registration, input) {
        const items = Array.from(input.files || [], (file) => ({
            file: file,
            path: file.webkitRelativePath || file.name,
        }));

        // Cleared, so that choosing the same files again still fires change. The File objects stay readable.
        input.value = "";
        notify(registration, items);
    }

    function dragEnter(registration, event) {
        if (!carriesFiles(event)) {
            return;
        }

        event.preventDefault();
        registration.depth++;
        acceptDrag(registration, event);
    }

    // Answered on every dragover rather than once on entry: the zone can be disabled while a drag hovers
    // over it, when an upload starts from a picker, and the cursor and the overlay have to follow. Being over
    // the zone at all also means the count is at least one, whatever enter event went missing.
    function dragOver(registration, event) {
        if (!carriesFiles(event)) {
            return;
        }

        event.preventDefault();
        registration.depth = Math.max(registration.depth, 1);
        acceptDrag(registration, event);
    }

    function acceptDrag(registration, event) {
        const disabled = isDisabled(registration.zone);

        event.dataTransfer.dropEffect = disabled ? refusedEffect : copyEffect;
        showOverlay(registration, !disabled);
    }

    // Counted, because moving onto a child of the zone fires dragleave on the zone itself; only the last one
    // means the pointer has actually left. The overlay ignores the pointer, so it never takes part.
    function dragLeave(registration, event) {
        if (!carriesFiles(event)) {
            return;
        }

        registration.depth = Math.max(registration.depth - 1, 0);

        if (registration.depth === 0) {
            showOverlay(registration, false);
        }
    }

    function drop(registration, event) {
        if (!carriesFiles(event)) {
            return;
        }

        event.preventDefault();
        registration.depth = 0;
        showOverlay(registration, false);

        if (isDisabled(registration.zone)) {
            return;
        }

        const captured = capture(event.dataTransfer);

        collect(captured).then(
            (items) => notify(registration, items),
            (error) => console.warn(`${logPrefix} the dropped files could not be read.`, error));
    }

    // Taken off the DataTransfer before the drop handler returns: the browser empties it then, and an entry
    // asked for afterwards is null. Only reading what the entries hold may wait.
    function capture(dataTransfer) {
        const list = dataTransfer.items;

        if (!list || list.length === 0 || typeof list[0].webkitGetAsEntry !== "function") {
            return Array.from(dataTransfer.files || [], (file) => ({ file: file, path: file.name }));
        }

        const captured = [];

        for (let i = 0; i < list.length; i++) {
            const item = list[i];

            if (item.kind !== fileKind) {
                continue;
            }

            // Null for something that is not on a file system to walk, such as an attachment dragged out of
            // another application; that still has its bytes.
            const entry = item.webkitGetAsEntry();
            const file = entry ? null : item.getAsFile();

            if (entry) {
                captured.push({ entry: entry });
            } else if (file) {
                captured.push({ file: file, path: file.name });
            }
        }

        return captured;
    }

    // In the order the drop listed them, whatever order the reads finish in.
    async function collect(captured) {
        const nested = await Promise.all(captured.map((candidate) => candidate.entry
            ? collectEntry(candidate.entry)
            : [{ file: candidate.file, path: candidate.path }]));

        return nested.flat();
    }

    // A dropped folder arrives as itself: its own name is the first segment of every path in it. An empty one
    // contributes nothing, because only files are uploaded. One that cannot be read is skipped, never fatal:
    // the rest of the drop still counts.
    async function collectEntry(entry) {
        if (entry.isFile) {
            try {
                const file = await fileOf(entry);
                return [{ file: file, path: entry.fullPath.replace(leadingSeparators, "") || file.name }];
            } catch (error) {
                console.warn(`${logPrefix} skipped '${entry.fullPath}', which could not be read.`, error);
                return [];
            }
        }

        if (entry.isDirectory) {
            const children = await childrenOf(entry);
            const nested = await Promise.all(children.map(collectEntry));
            return nested.flat();
        }

        return [];
    }

    function fileOf(entry) {
        return new Promise((resolve, reject) => entry.file(resolve, reject));
    }

    // readEntries hands a directory over in pieces — Chromium at most a hundred entries at a time — and says
    // it has finished with an empty one.
    async function childrenOf(directory) {
        const children = [];

        try {
            const reader = directory.createReader();
            let chunk;

            do {
                chunk = await new Promise((resolve, reject) => reader.readEntries(resolve, reject));
                children.push(...chunk);
            } while (chunk.length > 0);
        } catch (error) {
            console.warn(`${logPrefix} skipped the rest of '${directory.fullPath}', which could not be read.`,
                error);
        }

        return children;
    }

    // The same shape as clipboard.js's report: the call throws synchronously once the reference has been
    // disposed, and rejects when the circuit drops while it is in flight or when .NET fails. Either way
    // nobody is going to ask for these files any more. Sent even for an empty selection, so .NET can say so.
    function notify(registration, items) {
        // A drop still being read when its zone went away has nobody left to hand its files to.
        if (registrations.get(registration.zone) !== registration) {
            return;
        }

        const id = ++nextBatchId;
        batches.set(id, { zone: registration.zone, items: items });

        try {
            registration.dotNetRef.invokeMethodAsync(chosenCallback, id, items.length).catch(() => release(id));
        } catch {
            release(id);
        }
    }

    // Only for file drags no zone took: a zone's own listener has already called preventDefault.
    function guardDragOver(event) {
        if (event.defaultPrevented || !carriesFiles(event)) {
            return;
        }

        event.preventDefault();
        event.dataTransfer.dropEffect = refusedEffect;

        // The pointer is outside every zone, so no overlay should still be up, whichever dragleave the
        // browser failed to deliver.
        pruneDetached();
        for (const registration of registrations.values()) {
            registration.depth = 0;
            showOverlay(registration, false);
        }
    }

    function guardDrop(event) {
        if (event.defaultPrevented || !carriesFiles(event)) {
            return;
        }

        event.preventDefault();
    }

    // Up exactly while some zone is registered.
    function updateWindowGuard() {
        const needed = registrations.size > 0;

        if (needed === guarding) {
            return;
        }

        guarding = needed;

        if (needed) {
            window.addEventListener("dragover", guardDragOver);
            window.addEventListener("drop", guardDrop);
        } else {
            window.removeEventListener("dragover", guardDragOver);
            window.removeEventListener("drop", guardDrop);
        }
    }

    function listen(registration, target, type, listener) {
        target.addEventListener(type, listener);
        registration.listeners.push({ target: target, type: type, listener: listener });
    }

    function register(zone, filesInput, folderInput, dotNetRef) {
        if (!zone || !filesInput || !folderInput || !dotNetRef) {
            return;
        }

        unregister(zone);

        const registration = {
            zone: zone,
            filesInput: filesInput,
            folderInput: folderInput,
            dotNetRef: dotNetRef,
            depth: 0,
            listeners: [],
        };

        listen(registration, zone, "click", (event) => pick(registration, event));
        listen(registration, filesInput, "change", () => choose(registration, filesInput));
        listen(registration, folderInput, "change", () => choose(registration, folderInput));
        listen(registration, zone, "dragenter", (event) => dragEnter(registration, event));
        listen(registration, zone, "dragover", (event) => dragOver(registration, event));
        listen(registration, zone, "dragleave", (event) => dragLeave(registration, event));
        listen(registration, zone, "drop", (event) => drop(registration, event));

        registrations.set(zone, registration);
        updateWindowGuard();
    }

    function teardown(registration) {
        for (const { target, type, listener } of registration.listeners) {
            target.removeEventListener(type, listener);
        }

        showOverlay(registration, false);
        registrations.delete(registration.zone);

        for (const [id, batch] of batches) {
            if (batch.zone === registration.zone) {
                batches.delete(id);
            }
        }

        updateWindowGuard();
    }

    // A zone Blazor has already taken out of the page arrives here as null, so it could never be unregistered
    // by name; left alone, it would keep the window guard up for good.
    function pruneDetached() {
        for (const registration of Array.from(registrations.values())) {
            if (!registration.zone.isConnected) {
                teardown(registration);
            }
        }
    }

    function unregister(zone) {
        const registration = zone ? registrations.get(zone) : undefined;

        if (registration) {
            teardown(registration);
        }

        pruneDetached();
    }

    function batchOf(batchId) {
        const batch = batches.get(batchId);

        if (!batch) {
            throw new Error(`${logPrefix} batch ${batchId} is not held; it was released, or never chosen.`);
        }

        return batch;
    }

    function describe(batchId) {
        const manifest = batchOf(batchId).items.map((item) => ({ path: item.path, size: item.file.size }));
        return new Blob([JSON.stringify(manifest)], { type: manifestType });
    }

    function open(batchId, index) {
        const item = batchOf(batchId).items[index];

        if (!item) {
            throw new Error(`${logPrefix} batch ${batchId} holds no file ${index}.`);
        }

        return item.file;
    }

    function release(batchId) {
        batches.delete(batchId);
    }

    window.skillSuiteFileDrop = {
        register: register,
        unregister: unregister,
        describe: describe,
        open: open,
        release: release,
    };
})();
