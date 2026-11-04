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
    /// Opens a blob URL in a new browser tab.
    /// </summary>
    /// <param name="url">The blob URL.</param>
    public async Task OpenInNewTabAsync(string url) => await (await _module.Value).InvokeVoidAsync("openInNewTab", url);

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
    /// Shows a blob URL in an iframe.
    /// </summary>
    /// <param name="frame">The iframe.</param>
    /// <param name="url">The blob URL.</param>
    public async Task ShowInFrameAsync(ElementReference frame, string url) => await (await _module.Value).InvokeVoidAsync("showInFrame", frame, url);

    #endregion

    #endregion
}
