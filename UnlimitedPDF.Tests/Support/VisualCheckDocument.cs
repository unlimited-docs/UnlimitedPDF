using UnlimitedPDF.Enums;
using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Builds the PDF testers open on each validation viewer (WR-025, AC-18). Each image has a caption stating what a
/// correct viewer shows, so every check is a yes or no.
/// </summary>
internal static class VisualCheckDocument
{
    #region Fields and Constants

    /// <summary>
    /// Set to a folder path to write visual-check.pdf there when the visual-check test runs.
    /// </summary>
    public const string OutputVariable = "UNLIMITEDPDF_VISUAL_CHECK_DIR";

    private const string Quadrants = "red top-left, green top-right,|blue bottom-left, yellow bottom-right";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the visual-check document.
    /// </summary>
    public static PdfDocument Build()
    {
        var document = new PdfDocument { TopMargin = 36, BottomMargin = 36 };
        AddOrientationPage(document.AddPage());
        AddJpegColorPage(document.AddPage());
        AddPngPage(document.AddPage());
        AddPaginationPage(document.AddPage());
        return document;
    }

    #endregion

    #region Private Methods

    // Captions may hold several lines separated by '|', drawn 10 pt apart.
    private static void AddCaption(PdfPageBuilder page, string text, int x, int y)
    {
        var lines = text.Split('|');
        for (int i = 0; i < lines.Length; i++)
            page.AddText(lines[i], x, y - i * 10, 8);
    }

    private static void AddJpegColorPage(PdfPageBuilder page)
    {
        page.AddText("Visual check 2 of 4: JPEG color", 40, 800, 14);

        Place(page, "rgb-baseline.jpg", 40, 640, "RGB: " + Quadrants);
        Place(page, "gray-baseline.jpg", 300, 640, "Gray: black top-left, dark gray top-right,|light gray bottom-left, white bottom-right");
        Place(page, "cmyk-baseline.jpg", 40, 440, "CMYK (Adobe marker): cyan top-left, magenta top-right,|yellow bottom-left, black bottom-right; not a negative");

        // The file says 300 dpi; size ignores it, so this draws at the same size as rgb-baseline.jpg (72 x 48 pt).
        var highDpi = PdfImage.FromFile(Fixtures.Jpeg("rgb-300dpi.jpg"));
        page.AddImage(highDpi, 300, 470);
        AddCaption(page, "300 dpi file: drawn 72 x 48 pt,|1 x 2/3 inch, like a 96 dpi file", 300, 455);
    }

    // A table that continues on a library-added page with its header row repeated (WR-027).
    private static void AddPaginationPage(PdfPageBuilder page)
    {
        page.AddText("Visual check 4 of 4: table pagination", 40, 800, 14);
        AddCaption(page, "Rows 1 to 34 are on this page and none is cut at the bottom; the table continues on the|next page, which repeats the gray header row at its top, followed by rows 35 to 50 in order", 40, 785);

        var table = page.AddTable(40, 750, 500);
        table.SetColumnWidths(80, 270, 150);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = true;

        var header = table.AddRow();
        header.GetCell(0).Text = "Row";
        header.GetCell(1).Text = "Description";
        header.GetCell(2).Text = "Page";
        foreach (var cell in header.Cells) cell.Background = PdfColor.LightGray;

        for (int r = 1; r <= 50; r++)
        {
            var row = table.AddRow();
            row.GetCell(0).Text = r.ToString(CultureInfo.InvariantCulture);
            row.GetCell(1).Text = "Every cell has a full border";
            row.GetCell(2).Text = r <= 34 ? "first" : "continued";
        }
    }

    private static void AddOrientationPage(PdfPageBuilder page)
    {
        page.AddText("Visual check 1 of 4: EXIF orientation", 40, 800, 14);
        page.AddText("Every image must look the same:", 40, 780, 10);
        page.AddText("red top-left, green top-right, blue bottom-left, yellow bottom-right.", 40, 766, 10);

        for (int orientation = 1; orientation <= 8; orientation++)
        {
            int column = (orientation - 1) % 4;
            int row = (orientation - 1) / 4;
            int x = 40 + column * 130;
            int y = 640 - row * 140;
            var image = PdfImage.FromFile(Fixtures.Jpeg($"exif-orientation-{orientation}.jpg"));
            page.AddImage(image, x, y, width: 120);
            AddCaption(page, $"Orientation {orientation}", x, y - 14);
        }
    }

    private static void AddPngPage(PdfPageBuilder page)
    {
        page.AddText("Visual check 3 of 4: PNG", 40, 800, 14);
        page.AddText("Transparent parts must show the stripes behind them;", 40, 780, 10);
        page.AddText("no white or black boxes around the shapes.", 40, 766, 10);

        // Striped background drawn first, so the images on top must let it show through.
        var stripes = page.AddTable(40, 760, 520);
        stripes.RowHeight = 180;
        stripes.SetColumnWidths(Enumerable.Repeat(520.0 / 13, 13).ToArray());
        var row = stripes.AddRow();
        for (int c = 0; c < 13; c++)
            row.GetCell(c).Background = c % 2 == 0 ? new PdfColor(0.6, 0.85, 1) : new PdfColor(1, 0.9, 0.5);

        var samples = new (string Caption, PngBuilder Builder)[]
        {
            ("RGBA 8-bit: red disc", Disc(colorType: 6, depth: 8, interlaced: false)),
            ("RGBA 16-bit, interlaced: red disc", Disc(colorType: 6, depth: 16, interlaced: true)),
            ("Gray + alpha: dark gray disc", Disc(colorType: 4, depth: 8, interlaced: false)),
            ("Palette + tRNS: blue disc", PaletteDisc()),
            ("RGB color key: green square", ColorKeySquare()),
        };
        for (int i = 0; i < samples.Length; i++)
        {
            int x = 50 + i * 100;
            page.AddImage(PdfImage.FromBytes(samples[i].Builder.Build()), x, 620, 80, 80);
            page.AddText((i + 1).ToString(CultureInfo.InvariantCulture), x + 36, 596, 12);
            AddCaption(page, $"{i + 1}. {samples[i].Caption}", 40, 540 - i * 12);
        }

        // Opaque variants on white.
        var gradient = new PngBuilder
        {
            ColorType = 0, BitDepth = 16, Width = 64, Height = 16,
            SampleOverride = (x, y, c) => x * 65535 / 63
        };
        page.AddImage(PdfImage.FromBytes(gradient.Build()), 40, 420, 240, 40);
        AddCaption(page, "16-bit gray: smooth black-to-white ramp", 40, 406);

        var checker = new PngBuilder
        {
            ColorType = 0, BitDepth = 1, Width = 64, Height = 64,
            SampleOverride = (x, y, c) => (x / 8 + y / 8) % 2
        };
        page.AddImage(PdfImage.FromBytes(checker.Build()), 320, 380, 80, 80);
        AddCaption(page, "1-bit gray: 8 x 8 black-and-white|checkerboard", 320, 366);
    }

    private static PngBuilder ColorKeySquare() => new()
    {
        // Magenta (255, 0, 255) is the transparent key color around a green square.
        ColorType = 2, BitDepth = 8, Width = 64, Height = 64,
        Transparency = new byte[] { 0, 255, 0, 0, 0, 255 },
        SampleOverride = (x, y, c) =>
        {
            bool inside = x is >= 12 and < 52 && y is >= 12 and < 52;
            return inside ? (c == 1 ? 160 : 0) : (c == 1 ? 0 : 255);
        }
    };

    private static PngBuilder Disc(int colorType, int depth, bool interlaced)
    {
        int max = (1 << depth) - 1;
        return new PngBuilder
        {
            ColorType = colorType, BitDepth = depth, Interlaced = interlaced, Width = 64, Height = 64,
            SampleOverride = (x, y, c) =>
            {
                int alpha = DiscAlpha(x, y, max);
                return colorType == 6
                    ? c switch { 0 => max, 3 => alpha, _ => 0 }
                    : c == 0 ? max / 4 : alpha;
            }
        };
    }

    // Full inside radius 28, fading to transparent at radius 31.
    private static int DiscAlpha(int x, int y, int max)
    {
        double distance = Math.Sqrt((x - 31.5) * (x - 31.5) + (y - 31.5) * (y - 31.5));
        double coverage = Math.Clamp((31 - distance) / 3, 0, 1);
        return (int)Math.Round(coverage * max);
    }

    private static PngBuilder PaletteDisc() => new()
    {
        // Index 0 is fully transparent, index 1 is blue.
        ColorType = 3, BitDepth = 8, Width = 64, Height = 64,
        Palette = new byte[] { 0, 0, 0, 30, 90, 200 },
        Transparency = new byte[] { 0, 255 },
        SampleOverride = (x, y, c) => DiscAlpha(x, y, 1) > 0 ? 1 : 0
    };

    private static void Place(PdfPageBuilder page, string fixture, int x, int y, string caption)
    {
        var image = PdfImage.FromFile(Fixtures.Jpeg(fixture));
        page.AddImage(image, x, y, width: 180);
        AddCaption(page, caption, x, y - 14);
    }

    #endregion

    #endregion
}
