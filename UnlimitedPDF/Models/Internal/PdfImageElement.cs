namespace UnlimitedPDF.Models.Internal;

/// <summary>
/// One placement of an image on a page: the image and the box it is drawn in, in points.
/// </summary>
internal sealed class PdfImageElement
{
    #region Constructors and Finalizers

    public PdfImageElement(PdfImageData image, double x, double y, double width, double height)
    {
        Image = image;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the content stream operators that draw the image into its box.
    /// </summary>
    /// <param name="resourceName">The page resource name of the image, for example "/Im1".</param>
    /// <returns>The operators, for example "q 225 0 0 75 50 700 cm /Im1 Do Q".</returns>
    public string ToContent(string resourceName)
    {
        var (a, b, c, d, e, f) = OrientationMatrix(Image.Orientation, Width, Height);

        // q = save state, cm = map the image's unit square onto the box, Do = draw the image, Q = restore state
        return $"q {PdfNumber.Format(a)} {PdfNumber.Format(b)} {PdfNumber.Format(c)} {PdfNumber.Format(d)} " +
               $"{PdfNumber.Format(e + X)} {PdfNumber.Format(f + Y)} cm {resourceName} Do Q";
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the matrix that draws a stored image so it appears upright in a w x h box at the origin, for each of
    /// the eight EXIF orientations. The stored image's unit square has its top-left pixel at (0, 1).
    /// </summary>
    /// <param name="orientation">The EXIF orientation, 1 to 8.</param>
    /// <param name="w">The displayed width in points.</param>
    /// <param name="h">The displayed height in points.</param>
    /// <returns>The matrix entries a, b, c, d, e, f.</returns>
    internal static (double A, double B, double C, double D, double E, double F) OrientationMatrix(int orientation, double w, double h) =>
        orientation switch
        {
            2 => (-w, 0, 0, h, w, 0),  // Mirrored horizontally.
            3 => (-w, 0, 0, -h, w, h), // Rotated 180 degrees.
            4 => (w, 0, 0, -h, 0, h),  // Mirrored vertically.
            5 => (0, -h, -w, 0, w, h), // Mirrored along the top-left to bottom-right diagonal.
            6 => (0, -h, w, 0, 0, h),  // Turned 90 degrees clockwise for display.
            7 => (0, h, w, 0, 0, 0),   // Mirrored along the top-right to bottom-left diagonal.
            8 => (0, h, -w, 0, w, 0),  // Turned 90 degrees counterclockwise for display.
            _ => (w, 0, 0, h, 0, 0)    // Upright as stored.
        };

    #endregion

    #endregion

    #region Properties, Indexers

    public double Height { get; }

    public PdfImageData Image { get; }

    public double Width { get; }

    public double X { get; }

    public double Y { get; }

    #endregion
}
