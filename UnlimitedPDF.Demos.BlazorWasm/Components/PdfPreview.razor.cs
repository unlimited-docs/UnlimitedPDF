using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Services;
using UnlimitedPDF.Models.Internal.Rendering;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Shows a generated PDF drawn page by page by the library's renderer, and offers the same bytes for download or a new
/// tab. While a new PDF is generated, the previous one stays visible, dimmed under a progress overlay.
/// </summary>
public partial class PdfPreview : IAsyncDisposable
{
    #region Fields and Constants

    // Zoom steps, as CSS pixels per PDF point over 4/3 (1 is the page's actual size on screen).
    private static readonly double[] Steps = [0.5, 0.75, 1, 1.25, 1.5, 2, 3, 4];

    private static int s_instances;

    private readonly HashSet<int> _drawn = [];
    private readonly Dictionary<int, string> _failed = [];
    private readonly string _prefix = $"pv{Interlocked.Increment(ref s_instances)}-";
    private PdfFile? _file;
    private string _fileName = string.Empty;
    private DotNetObjectReference<PdfPreview>? _listener;
    private int _pageCount;
    private bool _rewatch;
    private double? _scale;
    private ElementReference _scroll;
    private int _shown;
    private IReadOnlyList<(double Width, double Height)> _sizes = [];
    private string? _unreadable;
    private string? _url;
    private int _version;
    private double _widest = 595;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _listener = DotNetObjectReference.Create(this);
            await Interop.InitViewerAsync(_scroll, _listener);
        }

        if (_rewatch)
        {
            _rewatch = false;
            await Interop.ScrollToPageAsync(_scroll, 1, smooth: false);
            await Interop.WatchPagesAsync(_scroll);
        }
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Interop.DisposeViewerAsync(_scroll);
            await Interop.RevokeObjectUrlAsync(_url);
        }
        catch (JSDisconnectedException)
        {
            // The page is closing; the browser releases the URL itself.
        }

        _url = null;
        _listener?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Saves the shown PDF under a file name.
    /// </summary>
    /// <param name="fileName">The file name to save as.</param>
    public async Task DownloadAsync(string fileName)
    {
        if (_url is not null) await Interop.DownloadAsync(_url, fileName);
    }

    /// <summary>
    /// Draws one page, when the viewer asks for it as it comes near the view. Called from <c>js/app.js</c>.
    /// </summary>
    /// <param name="number">The 1-based page number.</param>
    [JSInvokable]
    public async Task DrawPage(int number)
    {
        if (_file is null || number < 1 || number > _pageCount || _drawn.Contains(number) || _failed.ContainsKey(number)) return;

        int version = _version;
        var page = PdfPageRenderer.Render(_file, number - 1, $"{_prefix}v{version}-");
        if (version != _version) return;

        if (page.Svg is not null && await Interop.PlacePageAsync(_scroll, number, page.Svg))
        {
            _drawn.Add(number);
            return;
        }

        if (version != _version) return;
        _failed[number] = page.Reason ?? "The drawing can't be shown.";
        StateHasChanged();
    }

    /// <summary>
    /// Opens the shown PDF in a new browser tab.
    /// </summary>
    public async Task OpenInNewTabAsync()
    {
        if (_url is not null) await Interop.OpenInNewTabAsync(_url);
    }

    /// <summary>
    /// Notes the page in view, for the page number box. Called from <c>js/app.js</c>.
    /// </summary>
    /// <param name="number">The 1-based page number.</param>
    [JSInvokable]
    public void PageShown(int number)
    {
        if (number == _shown || number < 1 || number > _pageCount) return;
        _shown = number;
        StateHasChanged();
    }

    /// <summary>
    /// Sets the zoom after a two-finger pinch. Called from <c>js/app.js</c>.
    /// </summary>
    /// <param name="ratio">How much the fingers spread (above 1) or closed (below 1).</param>
    [JSInvokable]
    public async Task Pinched(double ratio)
    {
        if (_pageCount == 0 || !double.IsFinite(ratio) || ratio <= 0) return;
        double scale = await CurrentScaleAsync() * ratio;
        await SetScaleAsync(Math.Round(Math.Clamp(scale, Steps[0], Steps[^1]) * 20) / 20);
    }

    /// <summary>
    /// Shows a new PDF: its pages are laid out at their sizes and drawn as they come into view.
    /// </summary>
    /// <param name="pdfBytes">The PDF.</param>
    /// <param name="fileName">The file name for Download.</param>
    public async Task ShowAsync(byte[] pdfBytes, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        var previous = _url;
        _url = await Interop.CreateObjectUrlAsync(pdfBytes, "application/pdf");
        _fileName = fileName;
        _version++;
        _drawn.Clear();
        _failed.Clear();
        _shown = 1;
        _unreadable = null;
        try
        {
            _file = PdfFile.FromBytes(pdfBytes);
            _sizes = Enumerable.Range(0, _file.PageCount).Select(i => PdfPageRenderer.Size(_file, i) ?? (595.0, 842.0)).ToList();
            _pageCount = _sizes.Count;
            _widest = _sizes.Count == 0 ? 595 : _sizes.Max(s => s.Width);
        }
        catch (PdfFileException exception)
        {
            _file = null;
            _sizes = [];
            _pageCount = 0;
            _unreadable = exception.Message;
        }

        _rewatch = true;
        await Interop.RevokeObjectUrlAsync(previous);
        StateHasChanged();
    }

    #endregion

    #region Private Methods

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    // The zoom in use: the set one, or the one that makes the widest page fill the viewer.
    private async Task<double> CurrentScaleAsync()
    {
        if (_scale is { } scale) return scale;
        double width = await Interop.ViewerWidthAsync(_scroll);
        return width > 0 ? width / (_widest * 4 / 3) : 1;
    }

    private async Task FitWidthAsync() => await SetScaleAsync(null);

    private async Task GoToAsync(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return;
        number = Math.Clamp(number, 1, Math.Max(1, _pageCount));
        _shown = number;
        await Interop.ScrollToPageAsync(_scroll, number, smooth: true);
    }

    // + and = zoom in, - zooms out, and 0 fits the width, while the pages have focus.
    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.CtrlKey || args.MetaKey || args.AltKey) return;
        switch (args.Key)
        {
            case "+" or "=": await ZoomInAsync(); break;
            case "-": await ZoomOutAsync(); break;
            case "0": await FitWidthAsync(); break;
        }
    }

    private async Task OpenAtPageAsync(int number)
    {
        if (_url is not null) await Interop.OpenInNewTabAsync($"{_url}#page={number}");
    }

    // A new zoom keeps the page in view at the top.
    private async Task SetScaleAsync(double? scale)
    {
        _scale = scale;
        StateHasChanged();
        await Task.Yield();
        await Interop.ScrollToPageAsync(_scroll, Math.Max(1, _shown), smooth: false);
    }

    private async Task ZoomInAsync()
    {
        double scale = await CurrentScaleAsync();
        await SetScaleAsync(Steps.FirstOrDefault(step => step > scale + 0.01, Steps[^1]));
    }

    private async Task ZoomOutAsync()
    {
        double scale = await CurrentScaleAsync();
        await SetScaleAsync(Steps.LastOrDefault(step => step < scale - 0.01, Steps[0]));
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets whether a new PDF is being generated; the shown one is dimmed under a progress overlay meanwhile.
    /// </summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>
    /// Gets whether a PDF is shown.
    /// </summary>
    public bool HasPdf => _url is not null;

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    private string ZoomLabel => _scale is { } scale ? $"{Math.Round(scale * 100)}%" : "Fit";

    #endregion
}
