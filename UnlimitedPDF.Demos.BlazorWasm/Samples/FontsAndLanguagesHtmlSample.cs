using System.Globalization;
using System.Net;
using System.Text;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Fonts and languages, drawn with HTML: the same invoice as HTML, with the fonts you load registered under CSS family
/// names. Each font was loaded once with <c>PdfFont.FromStream(upload)</c>. A variable font covers its whole weight
/// range, so the body and heading weights are plain CSS <c>font-weight</c> values.
/// </summary>
public static class FontsAndLanguagesHtmlSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the invoice from HTML.
    /// </summary>
    /// <param name="input">The invoice and options.</param>
    /// <param name="fonts">The loaded fonts in slot order: each slot's script and its font.</param>
    /// <param name="html">The loaded HTML; after saving, its warnings list what was skipped or replaced.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(FontsAndLanguagesInput input, IReadOnlyList<(FontScript Script, PdfFont Font)> fonts, out PdfHtml html)
    {
        // Each font is registered under a CSS family name. A variable font draws each CSS weight as its own instance;
        // for a font with one weight, SynthesizeMissingStyles thickens it for bold headings.
        var options = new PdfHtmlLoadOptions { SynthesizeMissingStyles = input.SynthesizeMissingStyles };
        foreach (var (script, font) in fonts) options.Fonts.Add(Family(script), font);

        // The families are listed in slot order, so each character takes the first font that has it.
        string families = string.Concat(fonts.Select(f => $"'{Family(f.Script)}', ")) + "sans-serif";
        html = PdfHtml.FromString(Markup(input, families), options);

        // With FailOnMissingCharacters, saving stops on a character no font has instead of drawing it as '?'.
        var document = new PdfDocument { FailOnMissingCharacters = input.FailOnMissingCharacters };
        document.Body.AddHtml(html);
        return document;
    }

    /// <summary>
    /// Returns the CSS family name a slot's font is registered under, for example <c>Invoice Telugu</c>.
    /// </summary>
    /// <param name="script">The slot's script.</param>
    /// <returns>The family name.</returns>
    public static string Family(FontScript script) => "Invoice " + script;

    #endregion

    #region Private Methods

    // The invoice as HTML. Every typed value is HTML-encoded, so it is drawn as written and never adds markup (A05).
    private static string Markup(FontsAndLanguagesInput input, string families)
    {
        static string E(string text) => WebUtility.HtmlEncode(text);
        var invoice = input.Preset;
        var labels = invoice.Labels;
        var sb = new StringBuilder();
        sb.Append("<style>")
          // Line height 1.6 leaves room for marks drawn above and below Indic letters.
          .Append("body { font-family: ").Append(families).Append("; font-size: 10pt; line-height: 1.6; color: #1f2937; font-weight: ")
          .Append(input.BodyWeight.ToString(CultureInfo.InvariantCulture)).Append(" }")
          .Append("h1, b, .items th, .total td { font-weight: ").Append(input.HeadingWeight.ToString(CultureInfo.InvariantCulture)).Append(" }")
          .Append(".band { background-color: #e8f5ee; border-bottom: 3pt solid #168059; padding: 14pt 18pt }")
          .Append("h1 { font-size: 22pt; margin: 0 }")
          .Append(".parties { width: 100%; margin-top: 14pt } .parties td { vertical-align: top; width: 50% }")
          .Append(".items { width: 100%; border-collapse: collapse; margin-top: 12pt }")
          .Append(".items th, .items td { border: 0.75pt solid #1f2937; padding: 4pt 6pt }")
          .Append(".items th { background-color: #d1ebdd; text-align: left }")
          .Append(".num { text-align: right } .total td { background-color: #d1ebdd }")
          .Append(".thanks { margin-top: 16pt; color: #475569 }")
          .Append("</style>");

        sb.Append("<div class=\"band\"><h1>").Append(E(labels.Title)).Append("</h1></div>");
        sb.Append("<table class=\"parties\"><tr>")
          .Append("<td><b>").Append(E(labels.Seller)).Append("</b><br>").Append(E(input.SellerName)).Append("<br>").Append(E(input.SellerAddress)).Append("</td>")
          .Append("<td><b>").Append(E(labels.Buyer)).Append("</b><br>").Append(E(input.BuyerName)).Append("<br>").Append(E(input.BuyerAddress)).Append("</td>")
          .Append("</tr></table>");
        sb.Append("<p>").Append(E(labels.Number)).Append(": ").Append(E(input.InvoiceNumber)).Append("<br>")
          .Append(E(labels.Date)).Append(": ").Append(input.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append("</p>");

        sb.Append("<table class=\"items\"><thead><tr>")
          .Append("<th>").Append(E(labels.Item)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Quantity)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Rate)).Append("</th>")
          .Append("<th class=\"num\">").Append(E(labels.Amount)).Append("</th>")
          .Append("</tr></thead><tbody>");
        foreach (var line in input.Items)
        {
            sb.Append("<tr><td>").Append(E(line.Description)).Append("</td>")
              .Append("<td class=\"num\">").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append("</td>")
              .Append("<td class=\"num\">").Append(E(invoice.Money(line.Rate))).Append("</td>")
              .Append("<td class=\"num\">").Append(E(invoice.Money(line.Amount))).Append("</td></tr>");
        }
        sb.Append("<tr class=\"total\"><td colspan=\"3\">").Append(E(labels.Total)).Append("</td>")
          .Append("<td class=\"num\">").Append(E(invoice.Money(input.Total))).Append("</td></tr>")
          .Append("</tbody></table>");
        sb.Append("<p class=\"thanks\">").Append(E(labels.Thanks)).Append("</p>");
        return sb.ToString();
    }

    #endregion

    #endregion
}
