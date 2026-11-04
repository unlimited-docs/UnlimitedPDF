using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Demos.BlazorWasm.Components;

/// <summary>
/// Lets the visitor pick a built-in image or drop in their own JPEG or PNG. An upload shows reading progress, then a
/// thumbnail with its pixel and file size, and can be removed again.
/// </summary>
public partial class ImagePicker : IAsyncDisposable
{
    #region Fields and Constants

    private bool _dragOver;
    private string? _error;
    private string? _fileName;
    private long _fileSize;
    private double _readProgress;
    private bool _reading;
    private string? _thumbnailUrl;

    #endregion

    #region Methods

    #region Protected Override Methods

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // The demo cleared the upload (for example Reset to defaults): release its thumbnail too.
        if (Uploaded is null && _thumbnailUrl is not null)
        {
            await ReleaseThumbnailAsync();
        }
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await ReleaseThumbnailAsync();
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // The page is closing; the browser releases the URL itself.
        }
    }

    #endregion

    #region Private Methods

    private static string FormatSize(long bytes) =>
        bytes < 1024 * 1024
            ? string.Format(CultureInfo.InvariantCulture, "{0:0} KB", Math.Ceiling(bytes / 1024.0))
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} MB", bytes / (1024.0 * 1024.0));

    private async Task ChooseBuiltInAsync(string file)
    {
        _error = null;
        if (Uploaded is not null)
        {
            await RemoveUploadAsync();
        }

        await BuiltInFileChanged.InvokeAsync(file);
    }

    private async Task OnFileChosenAsync(InputFileChangeEventArgs args)
    {
        _dragOver = false;
        _error = null;
        _reading = true;
        _readProgress = 0;
        _fileName = args.File.Name;
        _fileSize = args.File.Size;
        StateHasChanged();

        var progress = new Progress<double>(value =>
        {
            _readProgress = value;
            StateHasChanged();
        });

        UploadResult result;
        try
        {
            result = await UploadedImageLoader.LoadAsync(args.File, progress);
        }
        catch (Exception exception)
        {
            // A browser read failure (for example a file removed from disk mid-read) must not leave the picker stuck.
            Console.Error.WriteLine(exception);
            result = UploadResult.Failed("This file couldn't be read. Try choosing it again.");
        }
        finally
        {
            _reading = false;
        }

        if (result.Image is null || result.Bytes is null)
        {
            _error = result.Error;
            return;
        }

        await ReleaseThumbnailAsync();
        var contentType = args.File.ContentType is { Length: > 0 } type ? type : "image/*";
        _thumbnailUrl = await Interop.CreateObjectUrlAsync(result.Bytes, contentType);

        // The bytes are no longer needed: the image holds its own processed data.
        await UploadedChanged.InvokeAsync(result.Image);
    }

    private async Task ReleaseThumbnailAsync()
    {
        var url = _thumbnailUrl;
        _thumbnailUrl = null;
        await Interop.RevokeObjectUrlAsync(url);
    }

    private async Task RemoveUploadAsync()
    {
        await ReleaseThumbnailAsync();
        _fileName = null;
        await UploadedChanged.InvokeAsync(null);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the selected built-in image file.
    /// </summary>
    [Parameter] public string BuiltInFile { get; set; } = "";

    /// <summary>
    /// Gets or sets the callback raised when a built-in image is chosen.
    /// </summary>
    [Parameter] public EventCallback<string> BuiltInFileChanged { get; set; }

    /// <summary>
    /// Gets or sets the built-in images to offer, as file name and label pairs.
    /// </summary>
    [Parameter, EditorRequired] public IReadOnlyList<(string File, string Label)> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the browser helpers.
    /// </summary>
    [Inject] private BrowserInterop Interop { get; set; } = default!;

    /// <summary>
    /// Gets or sets the uploaded image, or <see langword="null"/> when a built-in image is used.
    /// </summary>
    [Parameter] public PdfImage? Uploaded { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when an upload is loaded or removed.
    /// </summary>
    [Parameter] public EventCallback<PdfImage?> UploadedChanged { get; set; }

    #endregion
}
