using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Enums;
using UnlimitedPDF.Extensions;
using UnlimitedPDF.Models.Table;

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

        // Double frame: two one-cell tables with fixed row heights and colored borders.
        AddFrame(page, 28, pageHeight - 28, pageWidth - 56, pageHeight - 56, _gold, 3);
        AddFrame(page, 40, pageHeight - 40, pageWidth - 80, pageHeight - 80, _gold, 0.75);

        // The page paints in call order: the frames' white fill comes first, then the tinted title band,
        // then the text on top. Short gold bars underline the subtitle and the recipient's name.
        AddBand(page, 41, 722, pageWidth - 82, 94, _goldTint);
        AddBand(page, (pageWidth - 120) / 2, 645, 120, 2.5, _gold);
        AddBand(page, (pageWidth - 300) / 2, 538, 300, 1.5, _gold);

        AddCentered(page, input.Title, 690, 36, pageWidth, 0.62);
        AddCentered(page, input.Subtitle, 655, 16, pageWidth, 0.62);
        AddCentered(page, "This certificate is proudly presented to", 595, 12, pageWidth, 0.5);
        AddCentered(page, input.Recipient, 548, 30, pageWidth, 0.52);
        AddCentered(page, input.Description, 505, 12, pageWidth, 0.5);

        // Seal in the middle, signature on the right, both fitted into their boxes.
        var fit = new PdfImagePlacementOptions { Fit = PdfImageFit.Contain };
        page.AddImage(images["certificate-seal.png"], (pageWidth - 130) / 2, 300, 130, 130, fit);
        page.AddImage(images["certificate-signature.png"], 345, 205, 170, 50, fit);

        page.AddText("_____________________________", 345, 198, 10);
        page.AddText(PdfText.Escape(input.SignerName), 345, 182, 11);
        page.AddText(PdfText.Escape(input.SignerTitle), 345, 168, 9);

        page.AddText("_____________________", 85, 198, 10);
        page.AddText($"{input.Date:dd MMMM yyyy}", 85, 182, 11);
        page.AddText("Date", 85, 168, 9);

        return document;
    }

    #endregion

    #region Private Methods

    // AddText has no centering, so estimate the text width from an average character width (in ems).
    private static void AddCentered(PdfPageBuilder page, string text, int y, int fontSize, double pageWidth, double averageCharWidth)
    {
        int x = (int)Math.Max(48, (pageWidth - text.Length * fontSize * averageCharWidth) / 2);
        page.AddText(PdfText.Escape(text), x, y, fontSize);
    }

    // A filled rectangle drawn as a one-cell table. The border takes the fill color, because cells always draw one.
    private static void AddBand(PdfPageBuilder page, double x, double top, double width, double height, PdfColor color)
    {
        var band = page.AddTable(x, top, width);
        band.SetColumnWidths(width);
        band.RowHeight = height;
        var cell = band.AddRow().GetCell(0);
        cell.Background = color;
        foreach (var border in new[] { cell.LeftBorder, cell.TopBorder, cell.RightBorder, cell.BottomBorder })
        {
            border.Color = color;
        }
    }

    private static void AddFrame(PdfPageBuilder page, double x, double top, double width, double height, PdfColor color, double borderWidth)
    {
        var frame = page.AddTable(x, top, width);
        frame.SetColumnWidths(width);
        frame.RowHeight = height;
        var cell = frame.AddRow().GetCell(0);
        foreach (var border in new[] { cell.LeftBorder, cell.TopBorder, cell.RightBorder, cell.BottomBorder })
        {
            border.Color = color;
            border.Width = borderWidth;
        }
    }

    #endregion

    #endregion
}
