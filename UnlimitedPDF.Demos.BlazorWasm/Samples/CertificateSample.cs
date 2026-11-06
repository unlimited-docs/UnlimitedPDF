using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Certificate: a framed A4 page with centered text, a seal, and a signature image.
/// </summary>
public static class CertificateSample
{
    #region Fields and Constants

    private static readonly PdfColor _gold = new(0.72, 0.56, 0.2);
    private static readonly PdfColor _goldTint = new(0.99, 0.96, 0.88);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <param name="input">The certificate text.</param>
    /// <param name="images">The seal ("certificate-seal.png") and signature ("certificate-signature.png").</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(CertificateInput input, SampleImages images)
    {
        var document = new PdfDocument();
        var (pageWidth, pageHeight) = PdfPageSize.A4.ToDimensions();
        var page = document.AddPage();

        // Double frame: each frame is four thin gold rectangles along the page edges.
        AddFrame(page, 28, 28, pageWidth - 56, pageHeight - 56, 3);
        AddFrame(page, 40, 40, pageWidth - 80, pageHeight - 80, 0.75);

        // The tinted title band, and short gold bars under the subtitle and the recipient's name. Page content is
        // drawn first, so the centered text below sits on top of them.
        page.AddRectangle(41, 628, pageWidth - 82, 94, _goldTint);
        page.AddRectangle((pageWidth - 120) / 2, 642.5, 120, 2.5, _gold);
        page.AddRectangle((pageWidth - 300) / 2, 536.5, 300, 1.5, _gold);

        // The centered lines stack down the page in the body flow; each spacing is the gap above the line.
        var body = document.Body;
        document.BodyMarginTop = 118;
        body.AddText(input.Title, fontSize: 36, alignment: PdfTextAlignment.Center);
        body.AddText(input.Subtitle, spacing: 10, fontSize: 16, alignment: PdfTextAlignment.Center);
        body.AddText("This certificate is proudly presented to", spacing: 44, fontSize: 12, alignment: PdfTextAlignment.Center);
        body.AddText(input.Recipient, spacing: 16, fontSize: 30, alignment: PdfTextAlignment.Center);
        body.AddText(input.Description, spacing: 24, fontSize: 12, alignment: PdfTextAlignment.Center);

        // Seal in the middle, signature on the right, both fitted into their boxes.
        var fit = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        page.AddImage(images["certificate-seal.png"], (pageWidth - 130) / 2, 300, 130, 130, fit);
        page.AddImage(images["certificate-signature.png"], 345, 205, 170, 50, fit);

        page.AddText("_____________________________", 345, 198, 10);
        page.AddText(input.SignerName, 345, 182, 11);
        page.AddText(input.SignerTitle, 345, 168, 9);

        page.AddText("_____________________", 85, 198, 10);
        page.AddText($"{input.Date:dd MMMM yyyy}", 85, 182, 11);
        page.AddText("Date", 85, 168, 9);

        return document;
    }

    #endregion

    #region Private Methods

    // A frame of four filled rectangles: bottom, top, left, and right edges of the given box.
    private static void AddFrame(PdfPage page, double x, double y, double width, double height, double thickness)
    {
        page.AddRectangle(x, y, width, thickness, _gold);
        page.AddRectangle(x, y + height - thickness, width, thickness, _gold);
        page.AddRectangle(x, y, thickness, height, _gold);
        page.AddRectangle(x + width - thickness, y, thickness, height, _gold);
    }

    #endregion

    #endregion
}
