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
