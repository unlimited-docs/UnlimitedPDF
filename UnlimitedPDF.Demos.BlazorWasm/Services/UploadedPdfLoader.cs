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
    /// Loads <paramref name="file"/>, or returns a message saying why it cannot be used.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <returns>The loaded PDF, or null with a message.</returns>
    public static async Task<(PdfFile? File, string? Error)> LoadAsync(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Size > MaxFileBytes) return (null, "The file is larger than 20 MB.");

        try
        {
            await using var stream = file.OpenReadStream(MaxFileBytes);
            var pdf = await PdfFile.FromStreamAsync(stream, new PdfFileLoadOptions { MaxFileSize = MaxFileBytes });
            return (pdf, null);
        }
        catch (IOException)
        {
            return (null, "The file is larger than 20 MB.");
        }
        catch (PdfFileException exception)
        {
            return (null, exception.Reason switch
            {
                PdfFileErrorReason.Encrypted => "This PDF is encrypted, so it can't be merged.",
                PdfFileErrorReason.Empty => "This PDF has no pages.",
                PdfFileErrorReason.TooLarge => "This PDF is too large for the demo.",
                _ => "This file isn't a readable PDF.",
            });
        }
    }

    #endregion

    #endregion
}
