using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

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
    private bool _failed;
    private int _index;
    private bool _open;
    private bool _preparing;
    private DotNetObjectReference<DocumentQuickLook>? _self;

    #endregion

    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public void Dispose() => _self?.Dispose();

    /// <summary>
    /// Called by the browser when the dialog closes: by Escape, the Close button, or the page.
    /// </summary>
    [JSInvokable]
    public void OnDialogClosed()
    {
        _open = false;
        StateHasChanged();
    }

    /// <summary>
    /// Opens quick look on one document.
    /// </summary>
    /// <param name="index">The document's position in <see cref="DocumentGallery.Documents" />.</param>
    /// <returns>A task that completes when the dialog is shown.</returns>
    public async Task OpenAsync(int index)
    {
        _index = Math.Clamp(index, 0, DocumentGallery.Documents.Count - 1);
        _failed = false;
        _open = true;
        _self ??= DotNetObjectReference.Create(this);
        StateHasChanged();
        await Interop.ShowDialogAsync(_dialog, _self, nameof(OnDialogClosed));
    }

    #endregion

    #region Private Methods

    private async Task CloseAsync() => await Interop.CloseDialogAsync(_dialog);

    private async Task DownloadAsync()
    {
        if (_preparing || Current is not { } document) return;

        _preparing = true;
        _failed = false;
        StateHasChanged();
        try
        {
            // Let the button show that it is preparing before the document is built.
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
            _failed = true;
        }
        finally
        {
            _preparing = false;
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
        catch (JSDisconnectedException)
        {
            // The page has closed and released its blob URLs.
        }
    }

    // Previous on the first document shows the last, and next on the last shows the first (AC-18).
    private void Step(int delta)
    {
        int count = DocumentGallery.Documents.Count;
        _index = ((_index + delta) % count + count) % count;
        _failed = false;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    private GalleryDocument? Current => DocumentGallery.Documents.Count > 0 ? DocumentGallery.Documents[_index] : null;

    #endregion
}
