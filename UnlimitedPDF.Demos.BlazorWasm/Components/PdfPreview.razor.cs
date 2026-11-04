using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Shows a generated PDF in the browser's built-in PDF view, and offers the same bytes for download or a new tab.
/// While a new PDF is generated, the previous one stays visible, dimmed under a progress overlay.
/// </summary>
public partial class PdfPreview : IAsyncDisposable
{
    #region Fields and Constants

    private ElementReference _frame;
    private string? _url;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Interop.RevokeObjectUrlAsync(_url);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // The page is closing; the browser releases the URL itself.
        }

        _url = null;
    }

    /// <summary>
    /// Saves the current PDF under the given file name.
    /// </summary>
    /// <param name="fileName">The file name, for example "invoice.pdf".</param>
    public async Task DownloadAsync(string fileName)
    {
        if (_url is not null)
        {
            await Interop.DownloadAsync(_url, fileName);
        }
    }

    /// <summary>
    /// Opens the current PDF in a new browser tab.
    /// </summary>
    public async Task OpenInNewTabAsync()
    {
        if (_url is not null)
        {
            await Interop.OpenInNewTabAsync(_url);
        }
    }

    /// <summary>
    /// Shows a new PDF and releases the previous one.
    /// </summary>
    /// <param name="pdfBytes">The PDF content.</param>
    public async Task ShowAsync(byte[] pdfBytes)
    {
        var previous = _url;
        _url = await Interop.CreateObjectUrlAsync(pdfBytes, "application/pdf");
        await Interop.ShowInFrameAsync(_frame, _url);
        await Interop.RevokeObjectUrlAsync(previous);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets a value indicating whether a PDF is being generated.
    /// </summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>
    /// Gets a value indicating whether a PDF is shown.
    /// </summary>
    public bool HasPdf => _url is not null;

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    #endregion
}
