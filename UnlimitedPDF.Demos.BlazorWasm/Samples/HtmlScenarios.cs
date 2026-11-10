using System.Globalization;
using System.Text;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One known HTML to PDF problem, as a small document that shows it, and what this library does about it (WR-038).
/// </summary>
/// <param name="Id">The stable kebab-case id, used in the demo page's link.</param>
/// <param name="Group">The problem group, for example "Page breaks".</param>
/// <param name="Title">The short title.</param>
/// <param name="Problem">The problem as converters commonly show it.</param>
/// <param name="Outcome">What this library does with the HTML.</param>
/// <param name="IsLimitation">True when the outcome is a documented fallback rather than a fix.</param>
/// <param name="Html">The HTML that shows the problem.</param>
/// <param name="ExpectedWarnings">The warning kinds the conversion reports, in any order.</param>
public sealed record HtmlScenario(
    string Id,
    string Group,
    string Title,
    string Problem,
    string Outcome,
    bool IsLimitation,
    string Html,
    IReadOnlyList<PdfHtmlWarningKind> ExpectedWarnings);

/// <summary>
/// Known HTML to PDF problems gathered from developer reports about HTML converters, each as a scenario the
/// playground's "Tricky HTML" demo shows and the library's tests check. Content is project-authored (PR-011);
/// images are named "logo.png" and "banner.png" or written inline as <c>data:</c> URIs.
/// </summary>
public static class HtmlScenarios
{
    #region Fields and Constants

    // A 16 x 16 green PNG, written by the project.
    private const string GreenSquarePng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAFklEQVR42mPQmxdNEmIY1TCqYfhqAACofScQa/x9PAAAAABJRU5ErkJggg==";

    // A 120 x 60 SVG rectangle and the first bytes of a WebP file: formats the library does not draw.
    private const string ChartSvg = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxMjAiIGhlaWdodD0iNjAiPjxyZWN0IHdpZHRoPSIxMjAiIGhlaWdodD0iNjAiIGZpbGw9IiM0YTciLz48L3N2Zz4=";
    private const string PhotoWebp = "data:image/webp;base64,UklGRhoAAABXRUJQVlA4IA4AAAAAAAAAAAAAAAAAAAAAAA==";

    private const string Style = """
        <style>
          body { font-family: Helvetica, sans-serif; font-size: 10pt; color: #1f2937 }
          h1 { font-size: 15pt; color: #002A73; margin: 0 0 4pt }
          p.note { color: #4b5563; margin: 0 0 10pt }
          table { border-collapse: collapse }
          th { background-color: #002A73; color: white; text-align: left; padding: 3pt 5pt }
          td { border: 1px solid #d1d5db; padding: 3pt 5pt; vertical-align: top }
        </style>
        """;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    #endregion

    #region Properties

    /// <summary>
    /// Gets every scenario, grouped and in the order the demo page lists them.
    /// </summary>
    public static IReadOnlyList<HtmlScenario> All { get; } = Build();

    /// <summary>
    /// Gets the group names in the order the demo page lists them.
    /// </summary>
    public static IReadOnlyList<string> Groups { get; } = new[]
    {
        "Page breaks", "Tables", "Lists", "Text", "Images", "Links", "CSS", "Malformed HTML"
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the scenario with the id <paramref name="id"/>, matched exactly, or null.
    /// </summary>
    /// <param name="id">The id, for example from the page link; may be null.</param>
    /// <returns>The scenario, or null when no scenario has that id.</returns>
    public static HtmlScenario? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    #endregion

    #region Private Methods

    private static IReadOnlyList<HtmlScenario> Build() => new[]
    {
        // Page breaks
        Scenario("row-cut-in-half", "Page breaks", "Table row cut in half",
            "At a page break, a table row is cut through its text: the top half of the row is on one page and the bottom half on the next.",
            "Rows are never cut. A row that does not fit in the space left starts the next page whole.",
            false, Page("Order lines", "Each row has two lines of text. Look at the last row on page 1: it is complete, and the next row starts page 2.",
                "<table style=\"width:100%\"><tr><th>Order</th><th>Description</th><th>Amount</th></tr>"
                + Rows(45, i => $"<tr><td>SO-{1000 + i}</td><td>Replacement filter, size {i % 4 + 1}<br>Delivered to warehouse {i % 3 + 1}</td><td>{(i * 7.25m + 12).ToString("N2", Invariant)}</td></tr>")
                + "</table>")),
        Scenario("header-not-repeating", "Page breaks", "Header row not repeating",
            "A long table's header row is shown on the first page only, so later pages have columns without names.",
            "Rows inside <thead> repeat at the top of the table on every page it reaches.",
            false, Page("Transactions", "The blue header row appears at the top of the table on each page.",
                "<table style=\"width:100%\"><thead><tr><th>Date</th><th>Description</th><th>Amount</th></tr></thead><tbody>"
                + Rows(110, i => $"<tr><td>{new DateTime(2026, 1, 1).AddDays(i).ToString("dd MMM yyyy", Invariant)}</td><td>Card payment {i}</td><td>{(i * 3.5m).ToString("N2", Invariant)}</td></tr>")
                + "</tbody></table>")),
        Scenario("row-taller-than-page", "Page breaks", "Row taller than a page",
            "A single row taller than a page is clipped at the bottom of the page, or overflows past it.",
            "A row taller than the page splits between lines of text; no line is cut, and the row's borders are drawn on each part.",
            false, Page("Terms in one table cell", "The right-hand cell holds 90 lines. It continues on the next pages, line by line.",
                "<table style=\"width:100%\"><tr><td style=\"width:25%\">Terms</td><td>"
                + Rows(90, i => $"Clause {i}: the supplier keeps the goods insured until delivery.<br>")
                + "</td></tr></table>")),
        Scenario("break-inside-avoid", "Page breaks", "break-inside: avoid ignored",
            "A block marked break-inside: avoid (or page-break-inside: avoid) is still split across two pages.",
            "A block marked break-inside: avoid moves whole to the next page when it does not fit, if it fits on a page.",
            false, Page("Signature block", "The bordered signature block would cross the page end, so it starts page 2 whole.",
                Rows(57, i => $"<div>Line {i} of the agreement text.</div>")
                + "<div style=\"break-inside: avoid; border: 1px solid #9ca3af; padding: 6pt; margin-top: 6pt\">"
                + "<div><b>Signed for the buyer</b></div><div>Name: ____________________</div><div>Title: ____________________</div>"
                + "<div>Date: ____________________</div><div>Signature: ____________________</div><div>Place: ____________________</div></div>")),
        Scenario("forced-page-breaks", "Page breaks", "Forced page breaks ignored",
            "page-break-before: always and break-after: page are ignored, so chapters run on after each other.",
            "page-break-before, page-break-after, break-before, and break-after with always or page start a new page.",
            false, Page("Chapters", "Each chapter starts a new page, and the appendix follows on its own page.",
                "<h2>Chapter 1</h2><p>Introduction.</p>"
                + "<h2 style=\"page-break-before: always\">Chapter 2</h2><p>Method.</p>"
                + "<section style=\"break-before: page; break-after: page\"><h2>Chapter 3</h2><p>Results.</p></section>"
                + "<h2>Appendix</h2><p>Data tables.</p>")),
        Scenario("line-sliced", "Page breaks", "Line sliced across pages",
            "A line of text at the bottom of a page is drawn half on each page, with the bottom of its letters cut off.",
            "Lines of text are never cut; a paragraph continues on the next page at a line boundary.",
            false, Page("A long paragraph", "The paragraph fills more than a page; the last line on page 1 is whole.",
                "<p style=\"font-size: 13pt; line-height: 1.6\">"
                + Rows(80, i => $"Sentence {i} of a long paragraph that wraps over several lines. ")
                + "</p>")),
        Scenario("image-split", "Page breaks", "Image split across pages",
            "An image near the bottom of a page is cut in two, with its top on one page and its bottom on the next.",
            "An image that does not fit in the space left moves whole to the next page.",
            false, Page("Image near the page end", "The 300 px tall image does not fit under these lines, so it starts page 2.",
                Rows(42, i => $"<div>Paragraph {i} before the image.</div>")
                + "<img src=\"banner.png\" width=\"400\" height=\"300\" alt=\"Banner\">")),
        Scenario("blank-page-before-tall-content", "Page breaks", "Blank page before tall content",
            "Content taller than a page is pushed to a new page, leaving an empty page (or a page with only a heading) before it.",
            "Content taller than a page starts where it is and breaks as it goes, so no page is left empty; break-inside: avoid is dropped when the content cannot fit on one page anyway.",
            false, Page("Report with a long table", "The table is taller than a page and asks not to break, yet it starts right under this note.",
                "<table style=\"width:100%; break-inside: avoid\"><tr><th>Item</th><th>Count</th></tr>"
                + Rows(120, i => $"<tr><td>Item {i}</td><td>{i * 3}</td></tr>")
                + "</table>")),

        // Tables
        Scenario("nested-tables", "Tables", "Nested tables with spans",
            "Tables nested three levels deep, with colspan and rowspan, overlap each other or lose cells.",
            "Nested tables lay out inside their cells, spans included, and every cell's text stays inside its cell.",
            false, Page("Three levels", "Level 1 holds level 2, which holds level 3. Spanning cells are shaded.",
                "<table style=\"width:100%\"><tr><td colspan=\"2\" style=\"background-color:#e0e7ff\">Level 1 spans two columns</td></tr>"
                + "<tr><td style=\"width:30%\">Level 1 left</td><td>"
                + "<table style=\"width:100%\"><tr><td rowspan=\"2\" style=\"background-color:#fef3c7\">Level 2 spans two rows</td><td>Level 2 top</td></tr>"
                + "<tr><td><table style=\"width:100%\"><tr><td colspan=\"2\" style=\"background-color:#dcfce7\">Level 3 header</td></tr>"
                + "<tr><td>Level 3 A</td><td>Level 3 B</td></tr></table></td></tr></table>"
                + "</td></tr></table>")),
        Scenario("email-table-800px", "Tables", "800 px email table",
            "An HTML email built as a fixed 800 px wide table runs off the right edge of the page and is cut off.",
            "Tables wider than the page are narrowed to the page width; columns shrink in proportion and text wraps.",
            false, Page("Newsletter layout", "This table is 800 px wide (wider than the page) yet fits between the margins.",
                "<table width=\"800\" style=\"width:800px\"><tr><td colspan=\"3\" style=\"background-color:#002A73; color:white\">Monthly update for our customers</td></tr>"
                + "<tr><td width=\"260\">New products arrive in the spring range, with longer warranties.</td>"
                + "<td width=\"260\">Our service desk now opens at 7:00 on weekdays.</td>"
                + "<td width=\"280\">Visit us at the trade fair in hall 4, stand 12.</td></tr></table>")),
        Scenario("long-word-in-cell", "Tables", "Unbreakable word in a cell",
            "A long word or reference with no spaces widens its cell, pushing the table past the right edge of the page.",
            "A word wider than its cell breaks between characters inside the cell.",
            false, Page("Reference numbers", "The middle cell holds an 80-character reference with no spaces.",
                "<table style=\"width:100%\"><tr><td style=\"width:25%\">Invoice</td><td style=\"width:25%\">"
                + "REF-2026-ABCDEFGHIJKLMNOPQRSTUVWXYZ-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ-0123456789"
                + "</td><td>Paid in full on 3 March.</td></tr></table>")),
        Scenario("cell-styles-at-break", "Tables", "Cell styles lost at a page break",
            "When a row splits across pages, its cell backgrounds and borders are missing on one of the parts.",
            "A split row draws its cell backgrounds and borders on every part.",
            false, Page("Shaded cell across a page", "The shaded cell continues onto page 2 with its background and border.",
                "<table style=\"width:100%\"><tr><td style=\"width:30%; background-color:#fef3c7\">Notes</td><td style=\"background-color:#eef2ff\">"
                + Rows(75, i => $"Note {i}: inspected and approved.<br>")
                + "</td></tr></table>")),

        // Lists
        Scenario("numbering-across-pages", "Lists", "Numbering restarts on a new page",
            "An ordered list that crosses a page break restarts at 1, or its numbers disappear on the next page.",
            "List numbers run on across pages.",
            false, Page("Sixty steps", "The numbers continue from page 1 to page 2.",
                "<ol>" + Rows(60, i => $"<li>Step {i}: check the item and record the result.</li>") + "</ol>")),
        Scenario("mixed-nested-lists", "Lists", "Six-level mixed lists",
            "Deeply nested lists that mix numbers and bullets, with start, reversed, or value, show the wrong markers.",
            "Each level shows the marker its type and numbering attributes ask for.",
            false, Page("Nested outline", "Levels: 1. / disc / c. / circle / iii. reversed / 10. from value.",
                "<ol><li>Level one<ul><li>Level two<ol type=\"a\" start=\"3\"><li>Level three"
                + "<ul style=\"list-style-type:circle\"><li>Level four<ol type=\"i\" reversed><li>Level five A</li><li>Level five B"
                + "<ol><li value=\"10\">Level six from ten</li><li>Level six next</li></ol></li><li>Level five C</li></ol></li></ul>"
                + "</li><li>Level three next</li></ol></li></ul></li><li>Level one next</li></ol>")),
        Scenario("lists-in-cells", "Lists", "Lists inside table cells",
            "Lists inside table cells lose their markers or their indentation.",
            "Lists in cells keep their markers and indent from the cell's edge.",
            false, Page("Plan and checks", "Each cell holds a list.",
                "<table style=\"width:100%\"><tr><th>Plan</th><th>Checks</th></tr><tr>"
                + "<td><ol><li>Order parts</li><li>Assemble</li><li>Ship</li></ol></td>"
                + "<td><ul><li>Count parts</li><li>Test fit</li><li>Sign off</li></ul></td></tr></table>")),

        // Text
        Scenario("long-url", "Text", "Long URL runs off the page",
            "A long URL has no spaces to wrap at, so it runs past the right edge of the page and is cut off.",
            "A word wider than the line breaks between characters; each part of a link stays clickable to the same address.",
            false, Page("Download link", "The address below wraps over two lines, and both lines are one link.",
                "<p>Download: <a href=\"https://example.com/downloads/reports/2026/annual/customer-portfolio-summary-with-appendices-and-tables-final-version-v12.pdf\">"
                + "https://example.com/downloads/reports/2026/annual/customer-portfolio-summary-with-appendices-and-tables-final-version-v12.pdf</a></p>")),
        Scenario("wide-pre", "Text", "Preformatted text wider than the page",
            "Code or log lines in <pre> are wider than the page and are cut off at the right edge.",
            "<pre> keeps its lines unwrapped, as in a browser, so a line wider than the page is clipped at the margin and reported. Use white-space: pre-wrap to wrap it instead, as the second block does.",
            true, Page("Log output", "The first block is clipped at the right margin; the second wraps.",
                "<pre>2026-03-01 12:00:00 INFO  OrderService  Processed order SO-1001 for customer C-2002 in warehouse W-3 with 14 lines and 2 notes</pre>"
                + "<pre style=\"white-space: pre-wrap\">2026-03-01 12:00:00 INFO  OrderService  Processed order SO-1001 for customer C-2002 in warehouse W-3 with 14 lines and 2 notes</pre>"),
            PdfHtmlWarningKind.ContentClipped),
        Scenario("non-latin-characters", "Text", "Characters shown as boxes",
            "Accented letters, currency signs, or other scripts show as empty boxes or missing characters.",
            "Western European characters such as é, ü, and € are drawn with the standard fonts. Characters outside that set, such as Cyrillic, Chinese, and emoji, are drawn as ?.",
            true, Page("Names and prices", "The first line is drawn as written; the second line shows ? for each character outside the standard fonts.",
                "<p>Café Müller, Señora Ñúñez, Øresund: 25,00 € – “quoted”</p>"
                + "<p>Привет, 你好, 🙂</p>")),
        Scenario("nbsp-and-whitespace", "Text", "Non-breaking spaces and whitespace",
            "Amounts joined with &amp;nbsp; still break across lines, or runs of spaces and line breaks in the source show up as gaps.",
            "Words joined by &amp;nbsp; stay on one line, and runs of spaces and line breaks collapse to one space.",
            false, Page("Amounts in a narrow column", "The amount stays on one line; the spaced-out source text reads as one sentence.",
                "<table><tr><td style=\"width:90px\">Total:&nbsp;1&nbsp;250.00&nbsp;EUR</td></tr></table>"
                + "<p>Spaced      out\n      source     text</p>")),

        // Images
        Scenario("relative-and-data-images", "Images", "Relative and data: images missing",
            "Images named by a relative path or written as data: URIs come out blank.",
            "data: images are drawn, and named images are drawn when the base folder or resolver supplies them. A missing image shows its alt text and is reported.",
            false, Page("Three images", "A named logo, an inline data: image, and a missing photo that shows its alt text.",
                "<p><img src=\"logo.png\" width=\"120\" height=\"40\" alt=\"Company logo\"> "
                + $"<img src=\"{GreenSquarePng}\" width=\"40\" height=\"40\" alt=\"Green square\"></p>"
                + "<p><img src=\"photos/team-photo.jpg\" alt=\"Team photo\"></p>"),
            PdfHtmlWarningKind.ResourceNotResolved),
        Scenario("image-wider-than-page", "Images", "Image wider than the page",
            "A large image is drawn at its full size and runs off the right edge of the page.",
            "An image wider than the page is scaled down to the page width, keeping its proportions.",
            false, Page("Wide banner", "The image is 1200 px wide in the HTML and is scaled to fit between the margins.",
                "<img src=\"banner.png\" width=\"1200\" height=\"300\" alt=\"Banner\">")),
        Scenario("svg-and-webp", "Images", "SVG and WebP images",
            "SVG and WebP images are missing from the PDF, or the conversion fails.",
            "Only PNG and JPEG images are drawn. An SVG or WebP image shows its alt text and is reported.",
            true, Page("Chart and photo", "Both images show their alt text in place of the picture.",
                $"<p><img src=\"{ChartSvg}\" alt=\"[Sales chart]\"> <img src=\"{PhotoWebp}\" alt=\"[Product photo]\"></p>"),
            PdfHtmlWarningKind.UnsupportedImageFormat, PdfHtmlWarningKind.UnsupportedImageFormat),

        // Links
        Scenario("anchor-links", "Links", "Links inside the document",
            "Links to a part of the same document (#id) do nothing in the PDF.",
            "A #id link jumps to the page and position of the element with that id.",
            false, Page("Contents", "Click the link to jump to the terms on a later page.",
                "<p><a href=\"#terms\">Go to the terms and conditions</a></p>"
                + Rows(60, i => $"<div>Section text, line {i}.</div>")
                + "<h2 id=\"terms\">Terms and conditions</h2><p>Payment is due within 30 days.</p>")),
        Scenario("external-links", "Links", "External links",
            "Links are not clickable in the PDF, or javascript: links are kept.",
            "http, https, and mailto links are clickable. Other kinds of links, such as javascript:, are dropped and reported.",
            false, Page("Links", "The first two links are clickable; the third is dropped.",
                "<p><a href=\"https://example.com/pricing\">Pricing</a> · <a href=\"mailto:sales@example.com\">sales@example.com</a> · "
                + "<a href=\"javascript:alert(1)\">Run a script</a></p>"),
            PdfHtmlWarningKind.LinkDropped),

        // CSS
        Scenario("print-media", "CSS", "Print styles ignored",
            "@media print rules are ignored, or screen-only parts such as menus end up in the PDF.",
            "@media print and all rules apply; @media screen rules do not.",
            false,
            "<html><head>" + Style + "<style>.print-only { display: none } @media print { .print-only { display: block } .no-print { display: none } } @media screen { .screen-color { color: red } }</style></head><body>"
            + "<h1>Print styles</h1><p class=\"note\">The menu is hidden for print, and the print-only line is shown.</p>"
            + "<nav class=\"no-print\">Home | Products | Contact</nav><p class=\"print-only\">Printed copy: check the website for updates.</p><p class=\"screen-color\">This line is not red.</p></body></html>"),
        Scenario("flex-and-grid", "CSS", "Flexbox and grid layouts",
            "Pages built with flexbox or grid collapse, overlap, or lose their columns.",
            "Flexbox and grid are not supported: their items are laid out as stacked blocks, so nothing overlaps, and the layout is reported. Use tables for columns.",
            true, Page("Cards", "The three flex cards and two grid cells stack one under another.",
                "<div style=\"display: flex\"><div style=\"background-color:#e0e7ff; padding:6pt\">Card one</div>"
                + "<div style=\"background-color:#fef3c7; padding:6pt\">Card two</div><div style=\"background-color:#dcfce7; padding:6pt\">Card three</div></div>"
                + "<div style=\"display: grid; grid-template-columns: 1fr 1fr\"><div style=\"background-color:#fce7f3; padding:6pt\">Grid cell A</div>"
                + "<div style=\"background-color:#e5e7eb; padding:6pt\">Grid cell B</div></div>"),
            PdfHtmlWarningKind.UnsupportedValue, PdfHtmlWarningKind.UnsupportedValue, PdfHtmlWarningKind.UnsupportedProperty),
        Scenario("backgrounds", "CSS", "Background colors",
            "Background colors are missing, or they are drawn over the text and hide it.",
            "Background colors of blocks and cells are drawn behind their content. Background images are not supported; they are skipped and reported.",
            true, Page("Shaded panels", "Each panel's color is drawn behind its text; the panel's background image is skipped.",
                "<div style=\"background: #fffbe6 url(paper.png); padding: 6pt\">Panel with a color and an image</div>"
                + "<table style=\"width:100%; margin-top:6pt\"><tr><td style=\"background-color:#eef2ff\">Shaded cell</td><td>Plain cell</td></tr></table>"),
            PdfHtmlWarningKind.UnsupportedProperty),

        // Malformed HTML
        Scenario("malformed-html", "Malformed HTML", "Unclosed and misnested tags",
            "Sloppy HTML with unclosed or misnested tags loses text or breaks the conversion.",
            "The HTML is repaired as a browser repairs it, and all text is drawn.",
            false,
            "<h1>Sloppy HTML</h1><p>First <b>bold <i>bold italic</b> italic</i> plain<p>Second paragraph"
            + "<ul><li>Item one<li>Item two</ul><table><tr><td>Cell A<td>Cell B</table></div></span><p>Trailing text")
    };

    private static string Page(string heading, string note, string content) =>
        "<html><head>" + Style + "</head><body><h1>" + heading + "</h1><p class=\"note\">" + note + "</p>" + content + "</body></html>";

    private static string Rows(int count, Func<int, string> row)
    {
        var html = new StringBuilder();
        for (int i = 1; i <= count; i++) html.Append(row(i));
        return html.ToString();
    }

    private static HtmlScenario Scenario(string id, string group, string title, string problem, string outcome, bool isLimitation, string html, params PdfHtmlWarningKind[] warnings) =>
        new(id, group, title, problem, outcome, isLimitation, html, warnings);

    #endregion

    #endregion
}
