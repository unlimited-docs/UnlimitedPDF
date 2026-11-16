using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// A font a visitor uploaded to the Fonts and languages demo: its file name, its bytes (kept so a variable font can be
/// loaded again at another weight), the loaded font, and whether the visitor asserted the right to embed it.
/// </summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="Font">The font as loaded.</param>
/// <param name="RightsAsserted">Whether it was loaded with <see cref="PdfFontLoadOptions.IgnoreEmbeddingRestrictions"/>.</param>
public sealed record UploadedFont(string FileName, byte[] Bytes, PdfFont Font, bool RightsAsserted);

/// <summary>
/// The result of loading an uploaded font: the font, or a plain message, and whether the visitor may load it anyway by
/// asserting the right to embed it (D-028).
/// </summary>
/// <param name="Font">The loaded font, or null.</param>
/// <param name="Error">Why the font could not be loaded, or null.</param>
/// <param name="CanAssertRights">True when the font failed only because its license restricts embedding.</param>
public sealed record FontLoadResult(UploadedFont? Font, string? Error, bool CanAssertRights);

/// <summary>
/// Loads fonts visitors upload to the Fonts and languages demo, in the browser, up to the library's default size limit.
/// Every failure becomes a plain message; nothing is thrown to the page (D-028, A06, A10).
/// </summary>
public static class UploadedFontLoader
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads an uploaded font. The upload stream is read with the library's limit, because the browser upload's own
    /// default limit (500 KB) is smaller than most fonts.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="assertRights">Whether the visitor ticked "I have the right to embed this font".</param>
    /// <returns>The result.</returns>
    public static async Task<FontLoadResult> LoadAsync(IBrowserFile file, bool assertRights = false)
    {
        ArgumentNullException.ThrowIfNull(file);
        var (bytes, error) = await ReadAsync(file);
        return bytes is null ? new FontLoadResult(null, error, false) : Load(file.Name, bytes, assertRights);
    }

    /// <summary>
    /// Reads an uploaded file's bytes with the library's size limit, so a restricted font can be loaded again once
    /// the visitor asserts the rights.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <returns>The bytes, or null with the size message.</returns>
    public static async Task<(byte[]? Bytes, string? Error)> ReadAsync(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        long limit = PdfFont.DefaultMaxFileSize;
        if (file.Size > limit) return (null, Message(PdfFontErrorReason.TooLarge));
        try
        {
            await using var stream = file.OpenReadStream(limit);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return (buffer.ToArray(), null);
        }
        catch (IOException)
        {
            return (null, Message(PdfFontErrorReason.TooLarge));
        }
    }

    /// <summary>
    /// Loads a font from its bytes.
    /// </summary>
    /// <param name="fileName">The file name to show.</param>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="assertRights">Whether the visitor asserted the right to embed the font.</param>
    /// <returns>The result.</returns>
    public static FontLoadResult Load(string fileName, byte[] bytes, bool assertRights = false)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            var font = PdfFont.FromBytes(bytes, new PdfFontLoadOptions { IgnoreEmbeddingRestrictions = assertRights });
            return new FontLoadResult(new UploadedFont(fileName, bytes, font, assertRights), null, false);
        }
        catch (PdfFontException exception)
        {
            return Failed(exception.Reason);
        }
    }

    /// <summary>
    /// Returns the plain message for a reason a font could not be loaded.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The message.</returns>
    public static string Message(PdfFontErrorReason reason) => reason switch
    {
        PdfFontErrorReason.UnsupportedFormat => "This file isn't a .ttf or .otf font. Font collections (.ttc) and web fonts (WOFF, WOFF2) can't be loaded.",
        PdfFontErrorReason.TooLarge => string.Format(CultureInfo.InvariantCulture, "This font is larger than {0} MB.", PdfFont.DefaultMaxFileSize / (1024 * 1024)),
        PdfFontErrorReason.EmbeddingRestricted => "This font's license doesn't allow embedding it in a PDF.",
        _ => "This font can't be read. The file may be damaged."
    };

    /// <summary>
    /// Returns an uploaded variable font at a weight, clamped to its range, or the font itself when it has one weight.
    /// </summary>
    /// <param name="font">The uploaded font.</param>
    /// <param name="weight">The weight.</param>
    /// <returns>The font at that weight.</returns>
    public static PdfFont AtWeight(UploadedFont font, int weight)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!font.Font.IsVariable) return font.Font;
        int clamped = Math.Clamp(weight, font.Font.MinimumWeight, font.Font.MaximumWeight);
        if (clamped == font.Font.Weight) return font.Font;
        return PdfFont.FromBytes(font.Bytes, new PdfFontLoadOptions { Weight = clamped, IgnoreEmbeddingRestrictions = font.RightsAsserted });
    }

    #endregion

    #region Private Methods

    private static FontLoadResult Failed(PdfFontErrorReason reason) =>
        new(null, Message(reason), reason == PdfFontErrorReason.EmbeddingRestricted);

    #endregion

    #endregion
}
