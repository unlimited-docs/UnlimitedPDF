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
        const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        element.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "start" });
    }
}

export function focusFirstControl(id) {
    const element = document.getElementById(id);
    const control = element && element.querySelector("button, a[href]");
    if (control) {
        control.focus({ preventScroll: true });
    }
}

export function focusSkipTarget() {
    // The skip link's destination: the element a page marks with data-skip-target (the playground's demo, past its
    // sidebar), otherwise the main content. Only focus moves; the next Tab stop brings itself into view.
    const target = document.querySelector("[data-skip-target]") ?? document.getElementById("main-content");
    if (target) {
        target.focus({ preventScroll: true });
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

export function setDescription(text) {
    // Updates the one description tag from index.html, so a page never has two.
    let meta = document.head.querySelector('meta[name="description"]');
    if (!meta) {
        meta = document.createElement("meta");
        meta.name = "description";
        document.head.appendChild(meta);
    }
    meta.content = text;
}

// The site-wide search box (SiteSearchBox): Down and Up Arrow move between the results, Escape closes them and puts
// focus back in the box, and leaving the box and its results closes them too. Ctrl+K (Cmd+K on a Mac) focuses the
// visible search box, or opens the phone menu that holds one.
let searchShortcut = false;

export function initSiteSearch(root, listener) {
    if (!root || root.siteSearch) {
        return;
    }
    const input = root.querySelector(".site-search-input");
    const results = () => [...root.querySelectorAll(".site-search-results a")];
    const close = () => listener.invokeMethodAsync("CloseResults").catch(() => { });
    const onKeyDown = event => {
        const links = results();
        const at = links.indexOf(document.activeElement);
        if ((event.key === "ArrowDown" || event.key === "ArrowUp") && links.length > 0) {
            event.preventDefault();
            const step = event.key === "ArrowDown" ? 1 : -1;
            if (at === 0 && step < 0) {
                input.focus(); // Up from the first result goes back to the box.
                return;
            }
            const next = at < 0 ? (step > 0 ? 0 : links.length - 1) : (at + step + links.length) % links.length;
            links[next].focus();
        } else if (event.key === "Escape" && links.length + root.querySelectorAll(".site-search-empty").length > 0) {
            // Inside the phone menu this closes the results, not the menu.
            event.preventDefault();
            event.stopPropagation();
            close();
            input.focus();
        }
    };
    // Some browsers (Safari) don't focus a link when it is clicked, so focus leaves for nowhere while a result is
    // pressed; the results stay until the press ends.
    let pressing = false;
    const onPointerDown = () => { pressing = true; };
    const onPointerUp = () => { setTimeout(() => { pressing = false; }, 0); };
    const onFocusOut = event => {
        if (!pressing && !root.contains(event.relatedTarget)) {
            close();
        }
    };
    root.addEventListener("keydown", onKeyDown);
    root.addEventListener("focusout", onFocusOut);
    root.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("pointerup", onPointerUp);
    root.siteSearch = { onKeyDown, onFocusOut, onPointerDown, onPointerUp };

    if (!searchShortcut) {
        searchShortcut = true;
        document.addEventListener("keydown", event => {
            if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey || event.key.toLowerCase() !== "k") {
                return;
            }
            const box = [...document.querySelectorAll(".site-search-input")].find(element => element.offsetParent !== null);
            const menu = document.querySelector("[data-site-menu-open]");
            if (box) {
                event.preventDefault();
                box.focus();
                box.select();
            } else if (menu && menu.offsetParent !== null) {
                // On a phone the search is in the menu: open it, then put focus in its search box.
                event.preventDefault();
                menu.click();
                setTimeout(() => document.getElementById("site-menu-search")?.focus(), 50);
            }
        });
    }
}

export function disposeSiteSearch(root) {
    if (root && root.siteSearch) {
        root.removeEventListener("keydown", root.siteSearch.onKeyDown);
        root.removeEventListener("focusout", root.siteSearch.onFocusOut);
        root.removeEventListener("pointerdown", root.siteSearch.onPointerDown);
        document.removeEventListener("pointerup", root.siteSearch.onPointerUp);
        root.siteSearch = null;
    }
}

// The drawn PDF preview (PdfPreview). The library draws each page as SVG in .NET; this places it in the page, after
// checking it holds only what the renderer writes: a fixed set of elements and attributes, links to web pages, mail,
// or another page, and images as PNG or JPEG data. Anything else is dropped, so a page can never run a script or load
// from another site (A05). Pages are asked for as they come near the view, and the page in view is reported.
const svgElements = new Set(["svg", "defs", "rect", "path", "clipPath", "g", "text", "use", "image", "a", "title"]);
const svgAttributes = new Set([
    "xmlns", "viewBox", "width", "height", "x", "y", "d", "transform", "id", "fill", "fill-opacity", "fill-rule",
    "clip-path", "clip-rule", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
    "stroke-dasharray", "stroke-dashoffset", "vector-effect", "font-family", "font-size", "font-weight", "font-style",
    "font-variant", "textLength", "lengthAdjust", "xml:space", "preserveAspectRatio", "href", "aria-label", "target",
    "rel", "data-pdf-page"]);

function safeHref(element, value) {
    switch (element.localName) {
        case "use": return /^#[A-Za-z0-9_-]+$/.test(value);
        case "image": return /^data:image\/(png|jpeg);base64,[A-Za-z0-9+\/=]+$/.test(value);
        case "a": return /^#[A-Za-z0-9_-]+$/.test(value) || /^(https?:|mailto:)/i.test(value);
        default: return false;
    }
}

function cleanSvg(text) {
    const parsed = new DOMParser().parseFromString(text, "image/svg+xml");
    const root = parsed.documentElement;
    if (!root || root.localName !== "svg" || root.namespaceURI !== "http://www.w3.org/2000/svg" || parsed.getElementsByTagName("parsererror").length > 0) {
        return null;
    }
    for (const element of [...root.querySelectorAll("*")].concat(root)) {
        if (!svgElements.has(element.localName) || element.namespaceURI !== "http://www.w3.org/2000/svg") {
            element.remove();
            continue;
        }
        for (const attribute of [...element.attributes]) {
            // A reference to anything but an element of the drawing (url(...) in a paint or clip) is dropped, so the
            // page can't load from anywhere; ids keep to the renderer's letters, digits, hyphens, and underscores.
            const value = attribute.value;
            const ok = svgAttributes.has(attribute.name) && (attribute.name !== "href" || safeHref(element, value))
                && !/^\s*(javascript|data):/i.test(element.localName === "image" ? "" : value)
                && (!/url\s*\(/i.test(value) || /^url\(#[A-Za-z0-9_-]+\)$/.test(value))
                && (attribute.name !== "id" || /^[A-Za-z0-9_-]+$/.test(value));
            if (!ok) {
                element.removeAttribute(attribute.name);
            }
        }
    }
    root.removeAttribute("width");
    root.removeAttribute("height");
    return document.importNode(root, true);
}

export function placePage(scroll, number, svg) {
    const canvas = scroll && scroll.querySelector(`.viewer-page[data-page="${number}"] .viewer-canvas`);
    if (!canvas) {
        return false;
    }
    const drawing = cleanSvg(svg);
    if (!drawing) {
        return false;
    }
    canvas.replaceChildren(drawing);
    canvas.closest(".viewer-page").dataset.drawn = "true";
    return true;
}

export function viewerWidth(scroll) {
    if (!scroll) {
        return 0;
    }
    const style = getComputedStyle(scroll);
    return scroll.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
}

export function scrollToPage(scroll, number, smooth) {
    const page = scroll && scroll.querySelector(`.viewer-page[data-page="${number}"]`);
    if (page) {
        const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        const top = scroll.scrollTop + page.getBoundingClientRect().top - scroll.getBoundingClientRect().top - 8;
        scroll.scrollTo({ top, behavior: smooth && !reduced ? "smooth" : "auto" });
    }
}

export function initViewer(scroll, listener) {
    if (!scroll || scroll.viewer) {
        return;
    }
    const needed = new Set();
    let asking = false;
    let shown = 0;

    // Pages are drawn one at a time, nearest first, so a long document never blocks the page for long.
    const ask = async () => {
        if (asking) {
            return;
        }
        asking = true;
        while (needed.size > 0) {
            const next = Math.min(...needed);
            needed.delete(next);
            try {
                await listener.invokeMethodAsync("DrawPage", next);
            } catch {
                break;
            }
        }
        asking = false;
    };
    const near = new IntersectionObserver(entries => {
        for (const entry of entries) {
            const number = Number(entry.target.dataset.page);
            if (entry.isIntersecting && entry.target.dataset.drawn !== "true") {
                needed.add(number);
            } else {
                needed.delete(number);
            }
        }
        ask();
    }, { root: scroll, rootMargin: "600px 0px" });

    // The page in view: the one covering the line a third of the way down the viewer.
    const report = () => {
        const line = scroll.getBoundingClientRect().top + scroll.clientHeight / 3;
        let current = 1;
        for (const page of scroll.querySelectorAll(".viewer-page")) {
            if (page.getBoundingClientRect().top <= line) {
                current = Number(page.dataset.page);
            }
        }
        if (current !== shown) {
            shown = current;
            listener.invokeMethodAsync("PageShown", current).catch(() => { });
        }
    };
    let pending = false;
    const onScroll = () => {
        if (!pending) {
            pending = true;
            requestAnimationFrame(() => { pending = false; report(); });
        }
    };

    // A link to another page of the PDF scrolls there instead of leaving the site.
    const onClick = event => {
        const link = event.target.closest && event.target.closest("a[data-pdf-page]");
        if (link && scroll.contains(link)) {
            event.preventDefault();
            event.stopPropagation();
            scrollToPage(scroll, Number(link.getAttribute("data-pdf-page")), true);
        }
    };

    // Two fingers zoom the pages: the drawing scales while the fingers move, and the new zoom is set when they lift.
    const touches = new Map();
    let pinchStart = 0;
    let pinchRatio = 1;
    const distance = () => {
        const [a, b] = [...touches.values()];
        return Math.hypot(a.x - b.x, a.y - b.y);
    };
    const onPointerDown = event => {
        if (event.pointerType !== "touch") {
            return;
        }
        touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
        if (touches.size === 2) {
            pinchStart = distance();
            pinchRatio = 1;
        }
    };
    const onPointerMove = event => {
        if (!touches.has(event.pointerId)) {
            return;
        }
        touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
        if (touches.size === 2 && pinchStart > 0) {
            pinchRatio = Math.min(4, Math.max(0.25, distance() / pinchStart));
            scroll.style.setProperty("--pinch", String(pinchRatio));
        }
    };
    const onPointerEnd = event => {
        if (!touches.delete(event.pointerId)) {
            return;
        }
        if (touches.size < 2 && pinchStart > 0) {
            pinchStart = 0;
            scroll.style.removeProperty("--pinch");
            if (Math.abs(pinchRatio - 1) > 0.05) {
                listener.invokeMethodAsync("Pinched", pinchRatio).catch(() => { });
            }
        }
    };

    // While two fingers are down the browser must not pan or zoom the page itself, or it cancels the pinch.
    const onTouchMove = event => {
        if (event.touches.length === 2) {
            event.preventDefault();
        }
    };

    scroll.addEventListener("scroll", onScroll, { passive: true });
    scroll.addEventListener("touchmove", onTouchMove, { passive: false });
    scroll.addEventListener("click", onClick);
    scroll.addEventListener("pointerdown", onPointerDown);
    scroll.addEventListener("pointermove", onPointerMove);
    scroll.addEventListener("pointerup", onPointerEnd);
    scroll.addEventListener("pointercancel", onPointerEnd);
    scroll.viewer = { near, onScroll, onClick, onPointerDown, onPointerMove, onPointerEnd, onTouchMove, report };
}

export function watchPages(scroll) {
    // Called after the page list changes: every page that is not drawn yet is watched again.
    if (!scroll || !scroll.viewer) {
        return;
    }
    scroll.viewer.near.disconnect();
    scroll.querySelectorAll(".viewer-page").forEach(page => scroll.viewer.near.observe(page));
    scroll.viewer.report();
}

export function disposeViewer(scroll) {
    if (!scroll || !scroll.viewer) {
        return;
    }
    const viewer = scroll.viewer;
    viewer.near.disconnect();
    scroll.removeEventListener("scroll", viewer.onScroll);
    scroll.removeEventListener("click", viewer.onClick);
    scroll.removeEventListener("pointerdown", viewer.onPointerDown);
    scroll.removeEventListener("pointermove", viewer.onPointerMove);
    scroll.removeEventListener("pointerup", viewer.onPointerEnd);
    scroll.removeEventListener("pointercancel", viewer.onPointerEnd);
    scroll.removeEventListener("touchmove", viewer.onTouchMove);
    scroll.viewer = null;
}
