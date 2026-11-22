using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components.Tools;

/// <summary>
/// A component that shows page thumbnails drawn by the site's own renderer, each only as its tile comes near the view
/// (26112201 A-02). A tile is an element with <c>data-thumb</c> set to its key and a <c>.thumb-canvas</c> inside.
/// </summary>
public abstract class ThumbnailHost : ComponentBase, IAsyncDisposable
{
    #region Fields and Constants

    private static int _nextHost;
    private readonly int _host = Interlocked.Increment(ref _nextHost);
    private DotNetObjectReference<ThumbnailHost>? _listener;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Interop.StopThumbnailsAsync(Root);
        _listener?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Draws one tile's thumbnail, when the browser asks as the tile comes near the view. Called from <c>js/app.js</c>.
    /// </summary>
    /// <param name="key">The tile's key.</param>
    /// <returns>A task that completes when the drawing is placed.</returns>
    [JSInvokable]
    public async Task DrawThumbnail(string key)
    {
        if (Draw(key, $"t{_host}-{key}-") is { } svg) await Interop.PlaceThumbnailAsync(Root, key, svg);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Returns a tile's drawing, or null when there is nothing to draw.
    /// </summary>
    /// <param name="key">The tile's key.</param>
    /// <param name="idPrefix">A prefix for the drawing's ids, unique to the tile.</param>
    /// <returns>The drawing as SVG text, or null.</returns>
    protected abstract string? Draw(string key, string idPrefix);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _listener ??= DotNetObjectReference.Create<ThumbnailHost>(this);
        await Interop.ObserveThumbnailsAsync(Root, _listener);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] protected BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the element holding the tiles.
    /// </summary>
    protected ElementReference Root { get; set; }

    #endregion
}
