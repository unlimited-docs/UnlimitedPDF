namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Real-life HTML documents for HTML to PDF (WR-038): used by the playground's templates, the console demo, and the
/// library's golden tests. Images are referenced by name ("logo.png", "banner.png") and supplied by a resolver or a
/// base folder. All content is project-authored (PR-011).
/// </summary>
public static class HtmlTemplates
{
    #region Fields and Constants

    /// <summary>
    /// An invoice: logo, addresses, an item table with a nested option list, totals, and payment terms.
    /// </summary>
    public const string Invoice = """
        <!DOCTYPE html>
        <html><head><meta charset="utf-8"><title>Invoice INV-1042</title>
        <style>
          body { font-family: Helvetica, Arial, sans-serif; font-size: 10pt; color: #1f2937; margin: 0 }
          h1 { color: #002A73; font-size: 22pt; margin: 0 }
          .muted { color: #6b7280 }
          table.items { width: 100%; border-collapse: collapse; margin-top: 16pt }
          table.items th { background-color: #002A73; color: white; text-align: left; padding: 6pt }
          table.items td { border-bottom: 1px solid #d1d5db; padding: 6pt; vertical-align: top }
          .num { text-align: right }
          .total td { font-weight: bold; border-bottom: 2px solid #002A73 }
          ul.options { margin: 4pt 0 0; padding-left: 14pt; color: #6b7280; font-size: 9pt }
        </style></head>
        <body>
          <table width="100%"><tr>
            <td><img src="logo.png" width="120" alt="Northwind Supplies"></td>
            <td class="num"><h1>INVOICE</h1><div class="muted">INV-1042 &middot; 10 Nov 2026</div></td>
          </tr></table>
          <table width="100%" style="margin-top: 14pt"><tr>
            <td><b>Bill to</b><br>Acme Café Ltd<br>12 Harbour Street<br>Bristol BS1 4XE</td>
            <td><b>Ship to</b><br>Acme Café Ltd &ndash; Warehouse<br>Unit 7, Dock Road<br>Bristol BS2 0QT</td>
            <td class="num"><b>Due</b><br>30 Nov 2026<br><span class="muted">Net 20</span></td>
          </tr></table>
          <table class="items">
            <thead><tr><th>Item</th><th class="num">Qty</th><th class="num">Unit</th><th class="num">Amount</th></tr></thead>
            <tbody>
              <tr><td>Espresso machine <ul class="options"><li>Dual boiler</li><li>Stainless finish</li></ul></td><td class="num">1</td><td class="num">€1,450.00</td><td class="num">€1,450.00</td></tr>
              <tr><td>Burr grinder</td><td class="num">2</td><td class="num">€320.00</td><td class="num">€640.00</td></tr>
              <tr><td>Milk jugs, 600 ml</td><td class="num">6</td><td class="num">€14.50</td><td class="num">€87.00</td></tr>
              <tr><td>Installation and training <span class="muted">(half day)</span></td><td class="num">1</td><td class="num">€180.00</td><td class="num">€180.00</td></tr>
              <tr><td colspan="3" class="num">Subtotal</td><td class="num">€2,357.00</td></tr>
              <tr><td colspan="3" class="num">VAT 20%</td><td class="num">€471.40</td></tr>
              <tr class="total"><td colspan="3" class="num">Total due</td><td class="num">€2,828.40</td></tr>
            </tbody>
          </table>
          <p style="margin-top: 18pt"><b>Payment:</b> bank transfer to IBAN GB00 NWBK 0000 0000 0000 00, reference INV-1042.</p>
          <p class="muted" style="font-size: 8pt">Northwind Supplies &middot; 1 Market Way, London &middot; VAT GB 000 0000 00</p>
        </body></html>
        """;

    /// <summary>
    /// A newsletter laid out as an 800 px email: a banner, two-column story table, and nested lists.
    /// </summary>
    public const string Newsletter = """
        <html><head><style>
          body { margin: 0; background-color: #f3f4f6; font-family: Georgia, serif }
          .wrap { background-color: white }
          h2 { color: #00AC8C; font-family: Arial, sans-serif; margin: 0 0 6pt }
          a { color: #002A73 }
          td.story { padding: 10px; vertical-align: top }
        </style></head>
        <body>
          <table width="800" cellpadding="0" cellspacing="0" class="wrap" align="center">
            <tr><td><img src="banner.png" width="800" alt="Autumn news"></td></tr>
            <tr><td style="padding: 16px"><h1 style="font-family: Arial; margin: 0">Autumn news from the roastery</h1>
              <p>Three new single-origin coffees, a tasting evening, and a <a href="https://example.com/brewing">brewing guide</a>.</p></td></tr>
            <tr><td><table width="100%"><tr>
              <td class="story" width="50%"><h2>New this season</h2>
                <ol><li>Ethiopia Guji
                  <ul><li>Notes: peach, jasmine</li><li>Roast: light</li></ul></li>
                <li>Colombia Huila
                  <ul><li>Notes: caramel, red apple</li><li>Roast: medium</li></ul></li>
                <li>Sumatra Lintong</li></ol></td>
              <td class="story" width="50%"><h2>Tasting evening</h2>
                <p>Join us on <b>Thursday 20 November</b> at 7&nbsp;pm. Tickets are free; places are limited.</p>
                <dl><dt>Where</dt><dd>The Roastery, 4 Mill Lane</dd><dt>Bring</dt><dd>Your favourite mug</dd></dl></td>
            </tr></table></td></tr>
            <tr><td style="padding: 16px; font-size: 9pt; color: #6b7280">You receive this because you signed up at the shop. <a href="mailto:hello@example.com">Contact us</a>.</td></tr>
          </table>
        </body></html>
        """;

    /// <summary>
    /// A letter: sender and recipient blocks, date, justified paragraphs, and a signature.
    /// </summary>
    public const string Letter = """
        <html><head><style>
          body { font-family: 'Times New Roman', serif; font-size: 12pt; line-height: 1.4; margin: 24pt 36pt }
          .sender { text-align: right; color: #374151 }
          p { text-align: justify; text-indent: 18pt; margin: 0 0 8pt }
          p.plain { text-indent: 0 }
        </style></head>
        <body>
          <div class="sender">Dr. Hélène Martin<br>Rue de la Paix 18<br>1003 Lausanne</div>
          <p class="plain" style="margin-top: 24pt">Ms. Joanna Price<br>Head of Admissions<br>Northfield College<br>Cambridge CB2 1TN</p>
          <p class="plain" style="text-align: right">Lausanne, 10 November 2026</p>
          <p class="plain"><b>Re: Letter of recommendation for Mr. Tomás Ortega</b></p>
          <p class="plain">Dear Ms. Price,</p>
          <p>It is a pleasure to recommend Mr. Tomás Ortega for the master's programme in applied mathematics. I supervised his final-year project on numerical methods for coupled heat equations, which he completed with care, independence, and a rare clarity of writing.</p>
          <p>Mr. Ortega consistently went beyond what was asked. He rewrote our group's solver to run in half the time, documented every step, and taught two first-year students to use it. His questions in seminars were precise and often changed the direction of our discussions.</p>
          <p>I have no doubt that he will be an excellent member of your programme, and I recommend him without reservation. Please do not hesitate to contact me should you need further information.</p>
          <p class="plain" style="margin-top: 16pt">Yours sincerely,</p>
          <p class="plain" style="margin-top: 28pt"><i>Hélène Martin</i><br>Associate Professor of Mathematics</p>
        </body></html>
        """;

    /// <summary>
    /// A report: headings with ids, a contents list of internal links, nested tables with spans, lists, and notes.
    /// </summary>
    public const string Report = """
        <html><head><title>Q3 Operations Report</title><style>
          body { font-family: Helvetica, sans-serif; font-size: 10pt }
          h1 { color: #002A73 } h2 { color: #002A73; border-bottom: 1px solid #002A73 }
          table { border-collapse: collapse; width: 100% }
          th, td { border: 1px solid #9ca3af; padding: 4pt }
          th { background-color: #e5e7eb }
          .note { background-color: #fef3c7; border-left: 3pt solid #f59e0b; padding: 6pt; break-inside: avoid }
        </style></head>
        <body>
          <h1>Q3 Operations Report</h1>
          <p>Contents: <a href="#summary">Summary</a> &middot; <a href="#sites">Sites</a> &middot; <a href="#actions">Actions</a></p>
          <h2 id="summary">Summary</h2>
          <p>Output rose 6% on Q2 while energy use per unit fell 4%. Two sites missed their safety target; see <a href="#actions">actions</a>.</p>
          <h2 id="sites">Sites</h2>
          <table>
            <thead><tr><th rowspan="2">Site</th><th colspan="2">Output (t)</th><th rowspan="2">Detail</th></tr><tr><th>Q2</th><th>Q3</th></tr></thead>
            <tbody>
              <tr><td>Leeds</td><td>1,240</td><td>1,310</td><td><table><tr><th>Line</th><th>Uptime</th></tr><tr><td>A</td><td>97%</td></tr><tr><td>B</td><td>91%</td></tr></table></td></tr>
              <tr><td>Hull</td><td>860</td><td>905</td><td><ul><li>New kiln from August</li><li>Night shift added</li></ul></td></tr>
              <tr><td>York</td><td>530</td><td>548</td><td>No change</td></tr>
            </tbody>
          </table>
          <h3>Energy</h3>
          <ol><li>Heat recovery on line A<ol type="a"><li>Commissioned 4 July</li><li>Saves 120 MWh a year</li></ol></li><li>LED lighting in Hull</li></ol>
          <h2 id="actions">Actions</h2>
          <div class="note"><b>Safety:</b> Hull and York start weekly toolbox talks from 1 December. Owners report progress at each month end.</div>
          <h3>Next quarter</h3>
          <ul><li>Second shift in Leeds</li><li>Kiln maintenance window, 8&ndash;12 January</li><li>Supplier review<ul><li>Packaging</li><li>Logistics</li></ul></li></ul>
        </body></html>
        """;

    /// <summary>
    /// A résumé: a two-column layout table, headings, dated lists, and skills.
    /// </summary>
    public const string Resume = """
        <html><head><style>
          body { font-family: Arial, sans-serif; font-size: 10pt; color: #111827; margin: 0 }
          h1 { font-size: 24pt; margin: 0; color: #002A73 }
          h2 { font-size: 11pt; text-transform: uppercase; letter-spacing: 1pt; color: #00AC8C; margin: 12pt 0 4pt; border-bottom: 1px solid #d1d5db }
          td.side { width: 30%; background-color: #f3f4f6; padding: 10pt; vertical-align: top }
          td.main { padding: 10pt 0 10pt 14pt; vertical-align: top }
          .when { color: #6b7280; font-size: 9pt }
          ul { padding-left: 14pt; margin: 2pt 0 }
        </style></head>
        <body>
          <h1>Tomás Ortega</h1>
          <p style="margin: 2pt 0 10pt">Software engineer &middot; Madrid &middot; <a href="mailto:tomas@example.com">tomas@example.com</a></p>
          <table width="100%" cellspacing="0"><tr>
            <td class="side">
              <h2 style="margin-top: 0">Skills</h2>
              <ul><li>C# and .NET</li><li>Numerical methods</li><li>SQL and data modelling</li><li>Technical writing</li></ul>
              <h2>Languages</h2>
              <ul><li>Spanish (native)</li><li>English (C1)</li><li>French (B2)</li></ul>
            </td>
            <td class="main">
              <h2 style="margin-top: 0">Experience</h2>
              <p><b>Software engineer, Datos Abiertos</b><br><span class="when">2023 &ndash; today</span></p>
              <ul><li>Built a reporting service used by 40 city councils</li><li>Cut report generation time from minutes to seconds</li></ul>
              <p><b>Research assistant, EPFL</b><br><span class="when">2021 &ndash; 2023</span></p>
              <ul><li>Wrote a solver for coupled heat equations</li><li>Co-authored two conference papers</li></ul>
              <h2>Education</h2>
              <p><b>BSc Mathematics</b>, Universidad Complutense de Madrid <span class="when">2017 &ndash; 2021</span></p>
            </td>
          </tr></table>
        </body></html>
        """;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a multi-page account statement with a repeated header row.
    /// </summary>
    /// <param name="transactions">The number of transactions.</param>
    /// <returns>The HTML.</returns>
    public static string Statement(int transactions = 70)
    {
        var rows = new System.Text.StringBuilder();
        decimal balance = 2500m;
        for (int i = 1; i <= transactions; i++)
        {
            decimal amount = i % 3 == 0 ? 1250.75m : -(17.5m + i % 7 * 9.25m);
            balance += amount;
            string date = new DateTime(2026, 10, 1).AddDays(i / 3).ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            string text = i % 3 == 0 ? "Salary payment" : (i % 2 == 0 ? "Card payment &ndash; Market Hall" : "Direct debit &ndash; Utilities");
            rows.Append("<tr><td>").Append(date).Append("</td><td>").Append(text).Append("</td><td class=\"num\">")
                .Append(amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)).Append("</td><td class=\"num\">")
                .Append(balance.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)).Append("</td></tr>");
        }
        return """
            <html><head><style>
              body { font-family: Helvetica, sans-serif; font-size: 9pt; margin: 0 }
              h1 { font-size: 16pt; color: #002A73; margin: 0 0 4pt }
              table { width: 100%; border-collapse: collapse }
              th { background-color: #002A73; color: white; padding: 4pt; text-align: left }
              td { padding: 3pt 4pt; border-bottom: 1px solid #e5e7eb }
              tr.alt td { background-color: #f9fafb }
              .num { text-align: right }
            </style></head><body>
            <h1>Account statement</h1>
            <p>Account 12-34-56 87654321 &middot; 1 October to 31 October 2026</p>
            <table><thead><tr><th>Date</th><th>Description</th><th class="num">Amount</th><th class="num">Balance</th></tr></thead><tbody>
            """ + rows + "</tbody></table><p>Closing balance shown in EUR.</p></body></html>";
    }

    #endregion

    #endregion
}
