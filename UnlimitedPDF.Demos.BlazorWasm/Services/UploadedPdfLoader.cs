using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Loads a PDF the visitor uploads, in the browser, with a size limit that suits the demo.
/// </summary>
public static class UploadedPdfLoader
{
    #region Fields and Constants

    /// <summary>
    /// The largest upload the demo accepts: 20 MB.
    /// </summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads <paramref name="file"/> with the demo's 20 MB limit, or returns a message saying why it cannot be used.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <returns>The loaded PDF, or null with a message.</returns>
    public static Task<(PdfFile? File, string? Error)> LoadAsync(IBrowserFile file) =>
        LoadAsync(file, MaxFileBytes, "This PDF is encrypted, so it can't be merged.", "This PDF is too large for the demo.");

    /// <summary>
    /// Loads <paramref name="file"/> with the given size limit, as the free tools do, or returns a message saying why
    /// it cannot be used.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="maxBytes">The largest file accepted, in bytes.</param>
    /// <returns>The loaded PDF, or null with a message.</returns>
    public static Task<(PdfFile? File, string? Error)> LoadAsync(IBrowserFile file, long maxBytes) =>
        LoadAsync(file, maxBytes, "This PDF is encrypted, so it can't be opened here.", null);

    #endregion

    #region Private Methods

    private static async Task<(PdfFile? File, string? Error)> LoadAsync(IBrowserFile file, long maxBytes, string encrypted, string? libraryTooLarge)
    {
        ArgumentNullException.ThrowIfNull(file);
        string tooLarge = $"The file is larger than {Megabytes(maxBytes)}.";
        if (file.Size > maxBytes) return (null, tooLarge);

        try
        {
            await using var stream = file.OpenReadStream(maxBytes);
            var pdf = await PdfFile.FromStreamAsync(stream, new PdfFileLoadOptions { MaxFileSize = maxBytes });
            return (pdf, null);
        }
        catch (IOException)
        {
            return (null, tooLarge);
        }
        catch (PdfFileException exception)
        {
            return (null, exception.Reason switch
            {
                PdfFileErrorReason.Encrypted => encrypted,
                PdfFileErrorReason.Empty => "This PDF has no pages.",
                PdfFileErrorReason.TooLarge => libraryTooLarge ?? tooLarge,
                _ => "This file isn't a readable PDF.",
            });
        }
    }

    private static string Megabytes(long bytes) =>
        string.Create(CultureInfo.InvariantCulture, $"{(bytes + (1024 * 1024) - 1) / (1024 * 1024)} MB");

    #endregion

    #endregion
}
