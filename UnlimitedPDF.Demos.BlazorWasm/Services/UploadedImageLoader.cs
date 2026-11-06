using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Loads an image the visitor picked, entirely in the browser. Files are limited to 2 MB and 4096 x 4096 pixels so a
/// small but huge PNG cannot exhaust the tab's memory; nothing is stored or sent anywhere.
/// </summary>
public static class UploadedImageLoader
{
    #region Fields and Constants

    /// <summary>
    /// The largest accepted file, in bytes (2 MB).
    /// </summary>
    public const long MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>
    /// The most pixels an uploaded image may have (4096 x 4096).
    /// </summary>
    public const long MaxPixels = 4096L * 4096L;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads and loads an uploaded image.
    /// </summary>
    /// <param name="file">The picked file.</param>
    /// <param name="progress">Receives the share of the file read so far, from 0 to 1.</param>
    /// <returns>The loaded image and its bytes (for a thumbnail), or a readable error.</returns>
    public static async Task<UploadResult> LoadAsync(IBrowserFile file, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Size > MaxFileBytes)
        {
            return UploadResult.Failed("The file is larger than 2 MB.");
        }

        try
        {
            var bytes = new byte[file.Size];
            await using var stream = file.OpenReadStream(MaxFileBytes);
            int total = 0;
            int read;
            while (total < bytes.Length && (read = await stream.ReadAsync(bytes.AsMemory(total, Math.Min(64 * 1024, bytes.Length - total)))) > 0)
            {
                total += read;
                progress?.Report((double)total / bytes.Length);
            }

            var image = PdfImage.FromBytes(bytes, new PdfImageLoadOptions { MaxPixels = MaxPixels });
            return UploadResult.Loaded(image, bytes);
        }
        catch (IOException)
        {
            return UploadResult.Failed("The file is larger than 2 MB.");
        }
        catch (PdfImageException exception)
        {
            return UploadResult.Failed(exception.Reason switch
            {
                PdfImageErrorReason.UnsupportedFormat => "This file isn't a JPEG or PNG image.",
                PdfImageErrorReason.CorruptData => "The image data is damaged and can't be read.",
                PdfImageErrorReason.TooLarge => "The image is larger than 4096 x 4096 pixels.",
                _ => "This image can't be used."
            });
        }
        catch (ArgumentException)
        {
            return UploadResult.Failed("This image can't be used.");
        }
    }

    #endregion

    #endregion
}
