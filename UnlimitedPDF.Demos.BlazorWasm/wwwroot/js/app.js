// Browser helpers for the demo app. Generated PDFs and uploaded thumbnails stay in the browser as blob URLs; every
// URL created here is revoked by the caller when it is replaced or the page closes.

export async function createObjectUrl(streamRef, type) {
    const blob = new Blob([await streamRef.arrayBuffer()], { type });
    return URL.createObjectURL(blob);
}

export function revokeObjectUrl(url) {
    if (url) {
        URL.revokeObjectURL(url);
    }
}

export function showInFrame(frame, url) {
    if (frame) {
        // Fit the page width and hide the viewer's thumbnail pane so the page gets the room.
        frame.src = url + "#navpanes=0&view=FitH";
    }
}

function clickLink(url, configure) {
    const link = document.createElement("a");
    link.href = url;
    link.rel = "noopener";
    configure(link);
    document.body.appendChild(link);
    link.click();
    link.remove();
}

export function download(url, fileName) {
    clickLink(url, link => { link.download = fileName; });
}

export function openInNewTab(url) {
    clickLink(url, link => { link.target = "_blank"; });
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

export function scrollIntoViewOnNarrow(element) {
    if (element && window.matchMedia("(max-width: 1279.98px)").matches) {
        element.scrollIntoView({ behavior: "smooth", block: "start" });
    }
}

export function blurActiveElement() {
    // Commits a value being typed (its change event fires on blur) before a keyboard shortcut generates.
    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
}

export function showDialog(dialog, listener, closedMethod, opening) {
    // A modal dialog keeps focus inside, closes on Escape, and returns focus to the opener when it closes. The
    // listener is told when it closes, however that happens, with the latest opening: a second opening while the
    // dialog is already open replaces the number the close reports.
    if (!dialog) {
        return;
    }
    dialog.quickLookOpening = opening;
    if (!dialog.open) {
        dialog.addEventListener("close", () => listener.invokeMethodAsync(closedMethod, dialog.quickLookOpening), { once: true });
        dialog.showModal();
    }
}

export function closeDialog(dialog) {
    if (dialog && dialog.open) {
        dialog.close();
    }
}

// The section in view, for a page's topic index: on wide screens only, the last visible section whose top has passed a
// reading line about a third of the way down below the sticky top bar. The page leaves room after its last section so
// every section's top can reach that line. The listener hears each change.
let sectionWatch = null;

export function observeSections(sections, listener, method) {
    stopObservingSections();
    const wide = window.matchMedia("(min-width: 1024px)");
    let current = null;
    let pending = false;

    const update = () => {
        pending = false;
        if (!wide.matches) {
            return;
        }
        const topBar = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--app-topbar-h")) || 60;
        const line = topBar + Math.min(window.innerHeight * 0.3, 240);
        const visible = sections.filter(section => section && section.offsetParent !== null);
        if (visible.length === 0) {
            return;
        }
        let found = visible[0].id;
        for (const section of visible) {
            if (section.getBoundingClientRect().top > line) {
                break;
            }
            found = section.id;
        }
        if (found !== current) {
            current = found;
            listener.invokeMethodAsync(method, found).catch(() => { });
        }
    };
    // At most one check per 50 ms while scrolling; a timer rather than an animation frame, so it also runs in a tab
    // that is not painting.
    const schedule = () => {
        if (!pending) {
            pending = true;
            setTimeout(update, 50);
        }
    };

    window.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", schedule);
    wide.addEventListener("change", schedule);
    sectionWatch = { schedule, wide };
    schedule();
}

export function refreshSections() {
    sectionWatch?.schedule();
}

export function stopObservingSections() {
    if (sectionWatch) {
        window.removeEventListener("scroll", sectionWatch.schedule);
        window.removeEventListener("resize", sectionWatch.schedule);
        sectionWatch.wide.removeEventListener("change", sectionWatch.schedule);
        sectionWatch = null;
    }
}

export function scrollToElement(id, smooth) {
    // Brings an element to the top of the view; its scroll-margin keeps it below the top bar.
    const element = document.getElementById(id);
    if (!element) {
        return false;
    }
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    element.scrollIntoView({ behavior: smooth && !reduced ? "smooth" : "auto", block: "start" });
    return true;
}
