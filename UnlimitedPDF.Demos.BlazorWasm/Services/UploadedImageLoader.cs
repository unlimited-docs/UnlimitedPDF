using System.Globalization;
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
    /// Reads and loads an uploaded image with the demo's limits (2 MB, 4096 x 4096 pixels).
    /// </summary>
    /// <param name="file">The picked file.</param>
    /// <param name="progress">Receives the share of the file read so far, from 0 to 1.</param>
    /// <returns>The loaded image and its bytes (for a thumbnail), or a readable error.</returns>
    public static Task<UploadResult> LoadAsync(IBrowserFile file, IProgress<double>? progress = null) =>
        LoadAsync(file, MaxFileBytes, _ => MaxPixels, _ => "The image is larger than 4096 x 4096 pixels.", progress);

    /// <summary>
    /// Reads and loads an uploaded image with the free tools' limits: a JPEG may have more pixels than a PNG, because a
    /// JPEG goes into the PDF without being decoded while a PNG is decoded in full.
    /// </summary>
    /// <param name="file">The picked file.</param>
    /// <param name="maxBytes">The largest file accepted, in bytes.</param>
    /// <param name="maxJpegPixels">The most pixels a JPEG may have.</param>
    /// <param name="maxPngPixels">The most pixels a PNG may have.</param>
    /// <param name="progress">Receives the share of the file read so far, from 0 to 1.</param>
    /// <returns>The loaded image and its bytes, or a readable error.</returns>
    public static Task<UploadResult> LoadAsync(
        IBrowserFile file,
        long maxBytes,
        long maxJpegPixels,
        long maxPngPixels,
        IProgress<double>? progress = null) =>
        LoadAsync(
            file,
            maxBytes,
            bytes => IsJpeg(bytes) ? maxJpegPixels : maxPngPixels,
            bytes => IsJpeg(bytes)
                ? string.Create(CultureInfo.InvariantCulture, $"The JPEG is larger than {maxJpegPixels / 1_000_000} megapixels.")
                : PngTooLarge(maxPngPixels),
            progress);

    #endregion

    #region Private Methods

    private static bool IsJpeg(byte[] bytes) => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static string PngTooLarge(long maxPixels)
    {
        long side = (long)Math.Sqrt(maxPixels);
        return side * side == maxPixels
            ? string.Create(CultureInfo.InvariantCulture, $"The PNG is larger than {side} x {side} pixels.")
            : string.Create(CultureInfo.InvariantCulture, $"The PNG has more than {maxPixels} pixels.");
    }

    private static async Task<UploadResult> LoadAsync(
        IBrowserFile file,
        long maxBytes,
        Func<byte[], long> maxPixels,
        Func<byte[], string> tooManyPixels,
        IProgress<double>? progress)
    {
        ArgumentNullException.ThrowIfNull(file);
        string tooLarge = string.Create(CultureInfo.InvariantCulture, $"The file is larger than {maxBytes / (1024 * 1024)} MB.");
        if (file.Size > maxBytes)
        {
            return UploadResult.Failed(tooLarge);
        }

        byte[] bytes = [];
        try
        {
            bytes = new byte[file.Size];
            await using var stream = file.OpenReadStream(maxBytes);
            int total = 0;
            int read;
            while (total < bytes.Length && (read = await stream.ReadAsync(bytes.AsMemory(total, Math.Min(64 * 1024, bytes.Length - total)))) > 0)
            {
                total += read;
                progress?.Report((double)total / bytes.Length);
            }

            var image = PdfImage.FromBytes(bytes, new PdfImageLoadOptions { MaxPixels = maxPixels(bytes) });
            return UploadResult.Loaded(image, bytes);
        }
        catch (IOException)
        {
            return UploadResult.Failed(tooLarge);
        }
        catch (PdfImageException exception)
        {
            return UploadResult.Failed(exception.Reason switch
            {
                PdfImageErrorReason.UnsupportedFormat => "This file isn't a JPEG or PNG image.",
                PdfImageErrorReason.CorruptData => "The image data is damaged and can't be read.",
                PdfImageErrorReason.TooLarge => tooManyPixels(bytes),
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
