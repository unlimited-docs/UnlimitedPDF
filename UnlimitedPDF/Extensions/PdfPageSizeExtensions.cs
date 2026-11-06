using System.Globalization;

namespace UnlimitedPDF;

/// <summary>
/// Provides different extension methods for the <see cref="PdfPageSize"/> enum.
/// </summary>
public static class PdfPageSizeExtensions
{
    // Default PPI (points per inch) used in PDF measurements
    private const double DefaultPPI = 72.0;

    public static (double Width, double Height) ToDimensions(this PdfPageSize pageSize)
    {
        return pageSize switch
        {
            PdfPageSize.A0 => (2383.94, 3370.39),
            PdfPageSize.A1 => (1683.78, 2383.94),
            PdfPageSize.A2 => (1190.55, 1683.78),
            PdfPageSize.A3 => (841.89, 1190.55),
            PdfPageSize.A4 => (595.28, 841.89),
            PdfPageSize.A5 => (419.53, 595.28),
            PdfPageSize.A6 => (297.64, 419.53),

            PdfPageSize.Executive => (522.0, 756.0),
            PdfPageSize.Ledger => (1224.0, 792.0),
            PdfPageSize.Legal => (612.0, 1008.0),
            PdfPageSize.Letter => (612.0, 792.0),
            PdfPageSize.Tabloid => (792.0, 1224.0),

            PdfPageSize.B4 => (729.0, 1032.0),
            PdfPageSize.B5 => (516.0, 729.0),
            PdfPageSize.Statement => (396.0, 612.0),
            PdfPageSize.HalfLetter => (396.0, 612.0),

            _ => throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, null)
        };
    }

    /// <summary>
    /// Converts the specified <see cref="PdfPageSize"/> to a string representation of its media box
    /// dimensions.
    /// </summary>
    /// <remarks>The returned string specifies the lower-left and upper-right corners of the media box in a
    /// coordinate system where the origin is at the lower-left corner of the page.</remarks>
    /// <param name="pageSize">The page size to convert.</param>
    /// <returns>A string representing the media box dimensions in the format "[0 0 width height]".</returns>
    internal static string ToMediaBoxString(this PdfPageSize pageSize)
    {
        var (width, height) = pageSize.ToDimensions();
        return $"[0 0 {width.ToString(CultureInfo.InvariantCulture)} {height.ToString(CultureInfo.InvariantCulture)}]";
    }
}
