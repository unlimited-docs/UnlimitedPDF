using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Calls the app's own browser module (<c>js/app.js</c>): blob URLs for previews, downloads, and thumbnails, plus
/// clipboard and scrolling helpers. The module is loaded once, on first use.
/// </summary>
public sealed class BrowserInterop : IAsyncDisposable
{
    #region Fields and Constants

    private readonly Lazy<Task<IJSObjectReference>> _module;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserInterop"/> class.
    /// </summary>
    /// <param name="js">The JavaScript runtime.</param>
    public BrowserInterop(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _module = new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/app.js").AsTask());
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Ends a value being typed so its change event commits before a keyboard shortcut acts.
    /// </summary>
    public async Task BlurActiveElementAsync() => await (await _module.Value).InvokeVoidAsync("blurActiveElement");

    /// <summary>
    /// Closes a dialog opened with <see cref="ShowDialogAsync{T}"/>; the browser returns focus to its opener.
    /// </summary>
    /// <param name="dialog">The <c>dialog</c> element.</param>
    public async Task CloseDialogAsync(ElementReference dialog) => await (await _module.Value).InvokeVoidAsync("closeDialog", dialog);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns><see langword="true"/> when the browser allowed the copy.</returns>
    public async Task<bool> CopyTextAsync(string text) => await (await _module.Value).InvokeAsync<bool>("copyText", text);

    /// <summary>
    /// Creates a blob URL for bytes held by the app. The caller revokes it with <see cref="RevokeObjectUrlAsync"/>.
    /// </summary>
    /// <param name="bytes">The content.</param>
    /// <param name="contentType">The MIME type, for example "application/pdf".</param>
    /// <returns>The blob URL.</returns>
    public async Task<string> CreateObjectUrlAsync(byte[] bytes, string contentType)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var streamRef = new DotNetStreamReference(new MemoryStream(bytes, writable: false));
        return await (await _module.Value).InvokeAsync<string>("createObjectUrl", streamRef, contentType);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try
            {
                await (await _module.Value).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is closing; nothing to release.
            }
        }
    }

    /// <summary>
    /// Saves the content of a blob URL as a file.
    /// </summary>
    /// <param name="url">The blob URL.</param>
    /// <param name="fileName">The file name to save as.</param>
    public async Task DownloadAsync(string url, string fileName) => await (await _module.Value).InvokeVoidAsync("download", url, fileName);

    /// <summary>
    /// Moves keyboard focus to the skip link's destination: the element the page marks with <c>data-skip-target</c>,
    /// otherwise the main content.
    /// </summary>
    public async Task FocusSkipTargetAsync() => await (await _module.Value).InvokeVoidAsync("focusSkipTarget");

    /// <summary>
    /// Reports which section is in view, for a page's topic index: on screens 1,024 px and wider, the listener hears the
    /// ID of the last visible section whose top has passed a reading line about a third of the way down the view, each
    /// time it changes. A later call
    /// replaces this one.
    /// </summary>
    /// <typeparam name="T">The type of the component that listens.</typeparam>
    /// <param name="sections">The sections, in page order.</param>
    /// <param name="listener">The component told when the section in view changes.</param>
    /// <param name="method">The name of the listener's <see cref="JSInvokableAttribute"/> method; it receives the section's ID.</param>
    public async Task ObserveSectionsAsync<T>(ElementReference[] sections, DotNetObjectReference<T> listener, string method) where T : class =>
        await (await _module.Value).InvokeVoidAsync("observeSections", sections, listener, method);

    /// <summary>
    /// Opens a blob URL in a new browser tab.
    /// </summary>
    /// <param name="url">The blob URL.</param>
    public async Task OpenInNewTabAsync(string url) => await (await _module.Value).InvokeVoidAsync("openInNewTab", url);

    /// <summary>
    /// Checks the section in view again after sections were shown or hidden.
    /// </summary>
    public async Task RefreshSectionsAsync() => await (await _module.Value).InvokeVoidAsync("refreshSections");

    /// <summary>
    /// Scrolls a sidebar so its current demo's link is in view, without scrolling the page.
    /// </summary>
    /// <param name="container">The scrolling sidebar.</param>
    public async Task RevealActiveLinkAsync(ElementReference container) => await (await _module.Value).InvokeVoidAsync("revealActiveLink", container);

    /// <summary>
    /// Releases a blob URL created by <see cref="CreateObjectUrlAsync"/>.
    /// </summary>
    /// <param name="url">The blob URL, or <see langword="null"/> to do nothing.</param>
    public async Task RevokeObjectUrlAsync(string? url)
    {
        if (url is not null)
        {
            await (await _module.Value).InvokeVoidAsync("revokeObjectUrl", url);
        }
    }

    /// <summary>
    /// Scrolls an element into view when the screen is narrow (one-column layout).
    /// </summary>
    /// <param name="element">The element.</param>
    public async Task ScrollIntoViewOnNarrowAsync(ElementReference element) => await (await _module.Value).InvokeVoidAsync("scrollIntoViewOnNarrow", element);

    /// <summary>
    /// Scrolls an element to the top of the view; its <c>scroll-margin-top</c> keeps it below the top bar. Scrolling is
    /// instant when the visitor's system asks for reduced motion.
    /// </summary>
    /// <param name="id">The element's ID.</param>
    /// <param name="smooth">Whether to scroll smoothly rather than jump.</param>
    /// <returns><see langword="true"/> when the element exists.</returns>
    public async Task<bool> ScrollToElementAsync(string id, bool smooth) => await (await _module.Value).InvokeAsync<bool>("scrollToElement", id, smooth);

    /// <summary>
    /// Sets the content of the page's one meta description tag, adding the tag if it is missing.
    /// </summary>
    /// <param name="text">The description.</param>
    public async Task SetDescriptionAsync(string text) => await (await _module.Value).InvokeVoidAsync("setDescription", text);

    /// <summary>
    /// Opens a <c>dialog</c> element as a modal dialog and calls back when it closes.
    /// </summary>
    /// <typeparam name="T">The type of the component that listens.</typeparam>
    /// <param name="dialog">The <c>dialog</c> element.</param>
    /// <param name="listener">The component told when the dialog closes, by Escape or otherwise.</param>
    /// <param name="closedMethod">The name of the listener's <see cref="JSInvokableAttribute"/> method to call; it receives <paramref name="opening"/>.</param>
    /// <param name="opening">A number identifying this opening, passed back when it closes.</param>
    public async Task ShowDialogAsync<T>(ElementReference dialog, DotNetObjectReference<T> listener, string closedMethod, int opening) where T : class =>
        await (await _module.Value).InvokeVoidAsync("showDialog", dialog, listener, closedMethod, opening);

    /// <summary>
    /// Shows a blob URL in an iframe.
    /// </summary>
    /// <param name="frame">The iframe.</param>
    /// <param name="url">The blob URL.</param>
    public async Task ShowInFrameAsync(ElementReference frame, string url) => await (await _module.Value).InvokeVoidAsync("showInFrame", frame, url);

    /// <summary>
    /// Stops the reports started by <see cref="ObserveSectionsAsync{T}"/>.
    /// </summary>
    public async Task StopObservingSectionsAsync() => await (await _module.Value).InvokeVoidAsync("stopObservingSections");

    #endregion

    #endregion
}
