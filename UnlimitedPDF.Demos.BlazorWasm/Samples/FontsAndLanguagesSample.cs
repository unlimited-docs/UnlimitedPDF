using System.Globalization;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Fonts and languages, drawn with the text calls: an invoice in Hindi, Telugu, Tamil, French, or a mix, using the
/// fonts you load. Each font was loaded once with <c>PdfFont.FromStream(upload)</c>; a variable font was loaded again
/// with <c>new PdfFontLoadOptions { Weight = 700 }</c> for the headings.
/// </summary>
public static class FontsAndLanguagesSample
{
    #region Fields and Constants

    private const double Left = 50;

    private static readonly PdfColor _bandTint = new(0.91, 0.96, 0.93);
    private static readonly PdfColor _green = new(0.09, 0.50, 0.36);
    private static readonly PdfColor _headerTint = new(0.82, 0.92, 0.86);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the invoice.
    /// </summary>
    /// <param name="input">The invoice and options.</param>
    /// <param name="fonts">The loaded fonts, one per script, in slot order (Latin, Devanagari, Telugu, Tamil).</param>
    /// <param name="headingFonts">The same fonts at the heading weight (a font with one weight appears as itself).</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(FontsAndLanguagesInput input, IReadOnlyList<PdfFont> fonts, IReadOnlyList<PdfFont> headingFonts)
    {
        var invoice = input.Preset;
        var labels = invoice.Labels;

        // With FailOnMissingCharacters, saving stops on a character no font has instead of drawing it as '?'.
        var document = new PdfDocument { MarginBottom = 24, FailOnMissingCharacters = input.FailOnMissingCharacters };

        // No text below names a font, so each character is drawn with the first of these fonts that has it: one
        // font per script, in slot order. A Western European character none of them has uses the built-in font.
        foreach (var font in fonts) document.FontFallback.Add(font);

        // A header band with the title, drawn with a heading-weight font that has the title's letters.
        var (pageWidth, _) = PdfPageSize.A4.ToDimensions();
        document.Header.Height = 76;
        document.Header.AddRectangle(0, 0, pageWidth, 72, _bandTint);
        document.Header.AddRectangle(0, 72, pageWidth, 4, _green);
        if (HeadingFont(labels.Title, headingFonts) is { } titleFont) document.Header.AddText(labels.Title, titleFont, Left, 20, 22);
        else document.Header.AddText(labels.Title, Left, 20, 22);

        document.Footer.Height = 26;
        document.Footer.AddText(labels.Thanks, Left, 10, 9);
        document.Footer.AddText("{page} / {pages}", Left, 10, 9, PdfTextAlignment.Right);

        document.BodyMarginTop = 18;
        var body = document.Body;

        // Seller and buyer side by side, in a table without borders.
        var parties = body.AddTable(495, x: Left);
        parties.SetColumnWidths(248, 247);
        AddDetailRow(parties, labels.Seller, labels.Buyer, headingFonts);
        AddDetailRow(parties, input.SellerName, input.BuyerName, null);
        AddDetailRow(parties, input.SellerAddress, input.BuyerAddress, null);

        body.AddText($"{labels.Number}: {input.InvoiceNumber}", spacing: 14, fontSize: 10, x: Left);
        body.AddText($"{labels.Date}: {input.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}", fontSize: 10, x: Left);

        // Line items; the header row repeats on every page.
        var items = body.AddTable(495, spacing: 12, x: Left);
        items.SetColumnWidths(195, 80, 105, 115);
        items.HeaderRowCount = 1;
        items.RepeatHeaderRows = true;
        var header = items.AddRow();
        string[] headings = [labels.Item, labels.Quantity, labels.Rate, labels.Amount];
        for (int column = 0; column < headings.Length; column++)
        {
            header.GetCell(column).Text = headings[column];
            header.GetCell(column).BackgroundColor = _headerTint;
        }

        foreach (var line in input.Items)
        {
            var row = items.AddRow();
            row.HeightMode = PdfRowHeightMode.AtLeast;
            row.GetCell(0).Text = line.Description;
            SetRight(row.GetCell(1), line.Quantity.ToString(CultureInfo.InvariantCulture));
            SetRight(row.GetCell(2), invoice.Money(line.Rate));
            SetRight(row.GetCell(3), invoice.Money(line.Amount));
        }

        var total = items.AddRow();
        total.GetCell(0).Text = labels.Total;
        SetRight(total.GetCell(3), invoice.Money(input.Total));
        for (int column = 0; column < 4; column++) total.GetCell(column).BackgroundColor = _headerTint;

        return document;
    }

    /// <summary>
    /// Returns the characters of the invoice no font can draw, which would be drawn as '?'.
    /// </summary>
    /// <param name="input">The invoice.</param>
    /// <param name="fonts">The loaded fonts, in slot order.</param>
    /// <returns>The characters, each once, in order.</returns>
    public static IReadOnlyList<string> MissingCharacters(FontsAndLanguagesInput input, IReadOnlyList<PdfFont> fonts)
    {
        var document = new PdfDocument();
        foreach (var font in fonts) document.FontFallback.Add(font);
        var money = string.Concat(input.Items.Select(i => input.Preset.Money(i.Rate) + input.Preset.Money(i.Amount))) + input.Preset.Money(input.Total);
        return document.FindMissingCharacters(input.AllText() + money).Distinct().ToList();
    }

    #endregion

    #region Private Methods

    // A row of two texts without borders; a heading row uses a heading-weight font when one has its letters.
    private static void AddDetailRow(PdfTable table, string left, string right, IReadOnlyList<PdfFont>? headingFonts)
    {
        var row = table.AddRow();
        row.HeightMode = PdfRowHeightMode.AtLeast;
        row.Height = 16;
        string[] texts = [left, right];
        for (int column = 0; column < texts.Length; column++)
        {
            var cell = row.GetCell(column);
            if (headingFonts is not null && HeadingFont(texts[column], headingFonts) is { } font) cell.SetContent(new PdfCellText(texts[column], font));
            else cell.Text = texts[column];
            cell.Padding = 0;
            foreach (var border in new[] { cell.BorderLeft, cell.BorderTop, cell.BorderRight, cell.BorderBottom })
            {
                border.Color = PdfColor.White;
            }
        }
    }

    // The first heading font that has the letters of the text's scripts, so a Telugu title is drawn with the Telugu
    // font's heading weight; null when none has them, and the document's fonts draw it.
    private static PdfFont? HeadingFont(string text, IReadOnlyList<PdfFont> headingFonts)
    {
        var scripts = FontSlots.Needed(text);
        return headingFonts.FirstOrDefault(font => scripts.All(script => FontSlots.Covers(font, script)));
    }

    private static void SetRight(PdfCell cell, string text)
    {
        cell.Text = text;
        cell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
    }

    #endregion

    #endregion
}
