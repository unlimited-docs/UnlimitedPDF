using System.Globalization;
using Microsoft.AspNetCore.Components;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Shows a generated PDF in the browser's built-in PDF view, and offers the same bytes for download or a new tab.
/// While a new PDF is generated, the previous one stays visible, dimmed under a progress overlay. Browsers that don't
/// show a PDF placed in a frame (Android Chrome) get a "Your PDF is ready" card with Open PDF and Download instead, so
/// nothing downloads by itself (QF-26111303). iPhone and iPad show the PDF in the frame (QF-26111402).
/// </summary>
public partial class PdfPreview : IAsyncDisposable
{
    #region Fields and Constants

    private string _fileName = string.Empty;
    private ElementReference _frame;
    private bool? _inPage;
    private int _size;
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
    /// <param name="fileName">The PDF's file name, for example "invoice.pdf", shown on the ready card and used by its
    /// Download button.</param>
    public async Task ShowAsync(byte[] pdfBytes, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        _inPage ??= await Interop.CanShowPdfInPageAsync();

        var previous = _url;
        _url = await Interop.CreateObjectUrlAsync(pdfBytes, "application/pdf");
        _fileName = fileName;
        _size = pdfBytes.Length;
        if (_inPage == true)
        {
            await Interop.ShowInFrameAsync(_frame, _url);
        }

        await Interop.RevokeObjectUrlAsync(previous);
        StateHasChanged();
    }

    #endregion

    #region Private Methods

    // The same form as the size in the output's status line, for example "14.2 KB".
    private static string FormatSize(int bytes) =>
        bytes < 1024
            ? $"{bytes} B"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytes / 1024.0);

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

    // A PDF is ready, and this browser doesn't show a PDF placed in a frame.
    private bool ShowsCard => _url is not null && _inPage == false;

    #endregion
}
