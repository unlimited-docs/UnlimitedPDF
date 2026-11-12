using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Quick look for the home page gallery: one document at a time, stepping through all of them with wrap-around, and a
/// download built in the browser from the document's default input (AC-05, AC-15, AC-17, AC-18).
/// </summary>
public partial class DocumentQuickLook : IDisposable
{
    #region Fields and Constants

    // How long a download's blob URL is kept before it is released; the browser has started reading it by then.
    private static readonly TimeSpan DownloadUrlLifetime = TimeSpan.FromSeconds(30);

    private ElementReference _dialog;
    // The opening whose last download failed, if any.
    private int? _failedOpening;
    private int _index;
    private bool _open;
    // Counts the openings, so the close of an earlier opening, reported late, cannot empty a dialog opened again, and a
    // download's state shows only in the opening that started it.
    private int _opening;
    // The opening whose download is being built, if any.
    private int? _preparingOpening;
    private DotNetObjectReference<DocumentQuickLook>? _self;
    private bool _showPending;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The dialog opens once its content is on the page, so focus starts on its first control (Close).
        if (_showPending)
        {
            _showPending = false;
            _self ??= DotNetObjectReference.Create(this);
            await Interop.ShowDialogAsync(_dialog, _self, nameof(OnDialogClosed), _opening);
        }
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose() => _self?.Dispose();

    /// <summary>
    /// Called by the browser when the dialog closes: by Escape, the Close button, or the page.
    /// </summary>
    /// <param name="opening">The opening the closed dialog belonged to.</param>
    [JSInvokable]
    public void OnDialogClosed(int opening)
    {
        if (opening != _opening) return;
        _open = false;
        StateHasChanged();
    }

    /// <summary>
    /// Opens quick look on one document.
    /// </summary>
    /// <param name="index">The document's position in <see cref="DocumentGallery.Documents" />.</param>
    public void Open(int index)
    {
        _index = Math.Clamp(index, 0, DocumentGallery.Documents.Count - 1);
        _open = true;
        _opening++;
        _showPending = true;
        StateHasChanged();
    }

    #endregion

    #region Private Methods

    private async Task CloseAsync() => await Interop.CloseDialogAsync(_dialog);

    // The download belongs to the opening that started it: its preparing state and any failure show only there, and a
    // file asked for is still saved if quick look was closed meanwhile (F-12).
    private async Task DownloadAsync()
    {
        if (Preparing || Current is not { } document) return;

        int opening = _opening;
        _preparingOpening = opening;
        _failedOpening = null;
        try
        {
            // Let the button show that it is preparing before the document is built: Blazor renders the click's
            // changes when the handler first yields.
            await Task.Yield();
            var images = await ImageStore.GetManyAsync(document.Images.ToArray());
            using var stream = new MemoryStream();
            document.Build(images).Save(stream);

            var url = await Interop.CreateObjectUrlAsync(stream.ToArray(), "application/pdf");
            await Interop.DownloadAsync(url, document.Demo.FileName);
            _ = ReleaseLaterAsync(url);
        }
        catch (Exception exception) when (exception is not JSDisconnectedException)
        {
            // Any failure (an image that does not load, a build error) leaves quick look usable (AC-15).
            _failedOpening = opening;
        }
        finally
        {
            if (_preparingOpening == opening) _preparingOpening = null;
        }
    }

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "ArrowLeft") Step(-1);
        else if (args.Key == "ArrowRight") Step(1);
    }

    private async Task ReleaseLaterAsync(string url)
    {
        try
        {
            await Task.Delay(DownloadUrlLifetime);
            await Interop.RevokeObjectUrlAsync(url);
        }
        catch (Exception)
        {
            // The page has closed, or the browser could not release the URL; it is released with the page.
        }
    }

    // Previous on the first document shows the last, and next on the last shows the first (AC-18). While a download
    // builds, the document stays, so its preparing state and any failure message belong to the document shown.
    private void Step(int delta)
    {
        if (Preparing) return;
        int count = DocumentGallery.Documents.Count;
        _index = ((_index + delta) % count + count) % count;
        _failedOpening = null;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    private GalleryDocument? Current => DocumentGallery.Documents.Count > 0 ? DocumentGallery.Documents[_index] : null;

    // Whether the last download of the opening shown failed.
    private bool Failed => _failedOpening == _opening;

    // Whether the opening shown has a download being built.
    private bool Preparing => _preparingOpening == _opening;

    [Inject] private SampleImageStore ImageStore { get; set; } = default!;

    [Inject] private BrowserInterop Interop { get; set; } = default!;

    #endregion
}
