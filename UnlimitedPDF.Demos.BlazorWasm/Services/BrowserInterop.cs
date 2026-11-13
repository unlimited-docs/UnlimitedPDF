using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Calls the app's own browser module (<c>js/app.js</c>): blob URLs for previews, downloads, and thumbnails, plus
/// clipboard and scrolling helpers. The module is loaded once, on first use.
/// </summary>
/// <remarks>
/// Every call is guarded (BL0016, QF-26111205). A browser call that fails is ignored: an action does nothing, and a
/// question returns its "no" answer (for example <see cref="CopyTextAsync"/> returns <see langword="false"/>).
/// <see cref="CreateObjectUrlAsync"/> is the one exception, because there is no URL to return: it throws
/// <see cref="InvalidOperationException"/>, which its callers already show as a failed preview or download.
/// </remarks>
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
        _module = new(() => ImportModuleAsync(js));
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Ends a value being typed so its change event commits before a keyboard shortcut acts.
    /// </summary>
    public Task BlurActiveElementAsync() => InvokeVoidAsync("blurActiveElement");

    /// <summary>
    /// Asks whether the browser shows a PDF placed in a frame. Android Chrome downloads it instead (QF-26111303); iPhone
    /// and iPad show it (QF-26111402).
    /// </summary>
    /// <returns><see langword="true"/> when the browser shows PDFs inside the page, which is also the answer when the
    /// browser can't be asked.</returns>
    public Task<bool> CanShowPdfInPageAsync() => InvokeAsync("canShowPdfInPage", true);

    /// <summary>
    /// Closes a dialog opened with <see cref="ShowDialogAsync{T}"/>; the browser returns focus to its opener.
    /// </summary>
    /// <param name="dialog">The <c>dialog</c> element.</param>
    public Task CloseDialogAsync(ElementReference dialog) => InvokeVoidAsync("closeDialog", dialog);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns><see langword="true"/> when the browser allowed the copy.</returns>
    public Task<bool> CopyTextAsync(string text) => InvokeAsync("copyText", false, text);

    /// <summary>
    /// Creates a blob URL for bytes held by the app. The caller revokes it with <see cref="RevokeObjectUrlAsync"/>.
    /// </summary>
    /// <param name="bytes">The content.</param>
    /// <param name="contentType">The MIME type, for example "application/pdf".</param>
    /// <returns>The blob URL.</returns>
    /// <exception cref="InvalidOperationException">The browser could not create the URL.</exception>
    public async Task<string> CreateObjectUrlAsync(byte[] bytes, string contentType)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var streamRef = new DotNetStreamReference(new MemoryStream(bytes, writable: false));
        try
        {
            return await (await _module.Value).InvokeAsync<string>("createObjectUrl", streamRef, contentType);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException("The browser could not create a URL for the file.", exception);
        }
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
            catch (Exception exception) when (exception is JSDisconnectedException or JSException)
            {
                // The page is closing, or the module never loaded; nothing to release.
            }
        }
    }

    /// <summary>
    /// Saves the content of a blob URL as a file.
    /// </summary>
    /// <param name="url">The blob URL.</param>
    /// <param name="fileName">The file name to save as.</param>
    public Task DownloadAsync(string url, string fileName) => InvokeVoidAsync("download", url, fileName);

    /// <summary>
    /// Moves keyboard focus to the skip link's destination: the element the page marks with <c>data-skip-target</c>,
    /// otherwise the main content.
    /// </summary>
    public Task FocusSkipTargetAsync() => InvokeVoidAsync("focusSkipTarget");

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
    public Task ObserveSectionsAsync<T>(ElementReference[] sections, DotNetObjectReference<T> listener, string method) where T : class =>
        InvokeVoidAsync("observeSections", sections, listener, method);

    /// <summary>
    /// Opens a blob URL in a new browser tab.
    /// </summary>
    /// <param name="url">The blob URL.</param>
    public Task OpenInNewTabAsync(string url) => InvokeVoidAsync("openInNewTab", url);

    /// <summary>
    /// Checks the section in view again after sections were shown or hidden.
    /// </summary>
    public Task RefreshSectionsAsync() => InvokeVoidAsync("refreshSections");

    /// <summary>
    /// Scrolls a sidebar so its current demo's link is in view, without scrolling the page.
    /// </summary>
    /// <param name="container">The scrolling sidebar.</param>
    public Task RevealActiveLinkAsync(ElementReference container) => InvokeVoidAsync("revealActiveLink", container);

    /// <summary>
    /// Releases a blob URL created by <see cref="CreateObjectUrlAsync"/>.
    /// </summary>
    /// <param name="url">The blob URL, or <see langword="null"/> to do nothing.</param>
    public Task RevokeObjectUrlAsync(string? url) => url is null ? Task.CompletedTask : InvokeVoidAsync("revokeObjectUrl", url);

    /// <summary>
    /// Scrolls an element into view when the screen is narrow (one-column layout).
    /// </summary>
    /// <param name="element">The element.</param>
    public Task ScrollIntoViewOnNarrowAsync(ElementReference element) => InvokeVoidAsync("scrollIntoViewOnNarrow", element);

    /// <summary>
    /// Scrolls an element to the top of the view; its <c>scroll-margin-top</c> keeps it below the top bar. Scrolling is
    /// instant when the visitor's system asks for reduced motion.
    /// </summary>
    /// <param name="id">The element's ID.</param>
    /// <param name="smooth">Whether to scroll smoothly rather than jump.</param>
    /// <returns><see langword="true"/> when the element exists.</returns>
    public Task<bool> ScrollToElementAsync(string id, bool smooth) => InvokeAsync("scrollToElement", false, id, smooth);

    /// <summary>
    /// Sets the content of the page's one meta description tag, adding the tag if it is missing.
    /// </summary>
    /// <param name="text">The description.</param>
    public Task SetDescriptionAsync(string text) => InvokeVoidAsync("setDescription", text);

    /// <summary>
    /// Opens a <c>dialog</c> element as a modal dialog and calls back when it closes.
    /// </summary>
    /// <typeparam name="T">The type of the component that listens.</typeparam>
    /// <param name="dialog">The <c>dialog</c> element.</param>
    /// <param name="listener">The component told when the dialog closes, by Escape or otherwise.</param>
    /// <param name="closedMethod">The name of the listener's <see cref="JSInvokableAttribute"/> method to call; it receives <paramref name="opening"/>.</param>
    /// <param name="opening">A number identifying this opening, passed back when it closes.</param>
    public Task ShowDialogAsync<T>(ElementReference dialog, DotNetObjectReference<T> listener, string closedMethod, int opening) where T : class =>
        InvokeVoidAsync("showDialog", dialog, listener, closedMethod, opening);

    /// <summary>
    /// Shows a blob URL in an iframe.
    /// </summary>
    /// <param name="frame">The iframe.</param>
    /// <param name="url">The blob URL.</param>
    public Task ShowInFrameAsync(ElementReference frame, string url) => InvokeVoidAsync("showInFrame", frame, url);

    /// <summary>
    /// Stops the reports started by <see cref="ObserveSectionsAsync{T}"/>.
    /// </summary>
    public Task StopObservingSectionsAsync() => InvokeVoidAsync("stopObservingSections");

    #endregion

    #region Private Methods

    // Loads js/app.js. The module is only awaited inside the guarded calls below, so a failed load is caught there like
    // any failed call; BL0016 checks only where a call is written, so it is turned off for this one line.
    private static async Task<IJSObjectReference> ImportModuleAsync(IJSRuntime js) =>
#pragma warning disable BL0016
        await js.InvokeAsync<IJSObjectReference>("import", "./js/app.js");
#pragma warning restore BL0016

    // Calls a module function that returns a value; a failed call returns the fallback.
    private async Task<TValue> InvokeAsync<TValue>(string identifier, TValue fallback, params object?[] args)
    {
        try
        {
            return await (await _module.Value).InvokeAsync<TValue>(identifier, args);
        }
        catch (JSException)
        {
            return fallback;
        }
    }

    // Calls a module function that returns nothing; a failed call is ignored.
    private async Task InvokeVoidAsync(string identifier, params object?[] args)
    {
        try
        {
            await (await _module.Value).InvokeVoidAsync(identifier, args);
        }
        catch (JSException)
        {
            // The browser could not do it; the page carries on without it.
        }
    }

    #endregion

    #endregion
}
