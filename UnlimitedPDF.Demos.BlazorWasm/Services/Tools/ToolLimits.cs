using System.Globalization;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// The limits of the free tools, checked before any work starts so a run fits in a phone's browser memory (OWASP A06).
/// The scenarios keep their own, smaller limits. Every limit the pages state comes from here (PR-012).
/// </summary>
public static class ToolLimits
{
    #region Fields and Constants

    /// <summary>
    /// The largest PDF accepted, in bytes (50 MB).
    /// </summary>
    public const long MaxPdfBytes = 50L * Megabyte;

    /// <summary>
    /// The most files one run takes.
    /// </summary>
    public const int MaxFiles = 20;

    /// <summary>
    /// The most bytes one run takes in total (200 MB). A run holds its files, its result, and a copy of the result for
    /// the download at the same time, so its peak memory is about three times this.
    /// </summary>
    public const long MaxTotalBytes = 200L * Megabyte;

    /// <summary>
    /// The largest picture accepted, in bytes (10 MB).
    /// </summary>
    public const long MaxPictureBytes = 10L * Megabyte;

    /// <summary>
    /// The most pixels a JPEG may have (50 megapixels). JPEG data goes into the PDF without being decoded, so its memory
    /// cost is about its file size.
    /// </summary>
    public const long MaxJpegPixels = 50_000_000;

    /// <summary>
    /// The most pixels a PNG may have (4096 x 4096). PNG data is decoded in full, about four bytes a pixel.
    /// </summary>
    public const long MaxPngPixels = 4096L * 4096L;

    /// <summary>
    /// The largest HTML file accepted, in bytes (5 MB), as the HTML to PDF scenario.
    /// </summary>
    public const long MaxHtmlBytes = 5L * Megabyte;

    /// <summary>
    /// One megabyte, in bytes.
    /// </summary>
    public const long Megabyte = 1024L * 1024L;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a byte count as whole megabytes, rounded up, for messages: "150 MB".
    /// </summary>
    /// <param name="bytes">The byte count.</param>
    /// <returns>The size in megabytes.</returns>
    public static string Megabytes(long bytes) =>
        string.Create(CultureInfo.InvariantCulture, $"{(bytes + Megabyte - 1) / Megabyte} MB");

    /// <summary>
    /// Returns the message for a set of files over the total cap.
    /// </summary>
    /// <param name="totalBytes">The files' total size, in bytes.</param>
    /// <returns>The message.</returns>
    public static string TotalTooLarge(long totalBytes) =>
        $"These files add up to {Megabytes(totalBytes)}. One run takes up to {Megabytes(MaxTotalBytes)} in total.";

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the limits line for tools that take PDFs, as the pages state it.
    /// </summary>
    public static string PdfLimitsLine =>
        $"Up to {MaxFiles} files, {Megabytes(MaxPdfBytes)} each and {Megabytes(MaxTotalBytes)} in total.";

    /// <summary>
    /// Gets the limits line for a tool that takes one PDF.
    /// </summary>
    public static string SinglePdfLimitsLine => $"Up to {Megabytes(MaxPdfBytes)}.";

    /// <summary>
    /// Gets the limits line for pictures, as the pages state it.
    /// </summary>
    public static string PictureLimitsLine =>
        $"JPG or PNG, up to {Megabytes(MaxPictureBytes)} each; up to {MaxFiles} pictures and {Megabytes(MaxTotalBytes)} in total.";

    /// <summary>
    /// Gets the privacy line every tool page and the hub state.
    /// </summary>
    public static string PrivacyLine => "Your files never leave your device: everything runs right here in your browser.";

    #endregion
}
