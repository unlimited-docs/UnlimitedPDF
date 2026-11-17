using System.Net;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Contract: an employment agreement written in HTML, with numbered clauses and sub-clauses, long paragraphs that wrap
/// across pages, a signature block kept together, and "Page X of Y" in the footer.
/// </summary>
public static class ContractSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Builds the contract. The visitor's values are encoded before they go into the HTML.
    /// </summary>
    /// <param name="input">The parties, the role, the start date, and the salary.</param>
    /// <returns>The document, ready to save.</returns>
    public static PdfDocument Build(ContractInput input)
    {
        var html = PdfHtml.FromString(Html(input));

        var document = new PdfDocument { MarginTop = 48, MarginBottom = 40, MarginLeft = 60, MarginRight = 60 };
        document.Footer.Height = 20;
        document.Footer.AddText("Employment agreement", 0, 6, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        document.Body.AddHtml(html);
        return document;
    }

    /// <summary>
    /// Writes the contract's HTML.
    /// </summary>
    /// <param name="input">The parties, the role, the start date, and the salary.</param>
    /// <returns>The HTML document.</returns>
    public static string Html(ContractInput input)
    {
        string employer = WebUtility.HtmlEncode(input.EmployerName);
        string employee = WebUtility.HtmlEncode(input.EmployeeName);
        string role = WebUtility.HtmlEncode(input.Role);
        string start = input.StartDate.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        string salary = input.AnnualSalary.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        return $$"""
            <html><head><style>
              body { font-family: Helvetica, sans-serif; font-size: 10.5pt; line-height: 1.45; color: #1a1a1a; }
              h1 { font-size: 18pt; text-align: center; margin: 0 0 4pt; }
              .sub { text-align: center; color: #555; margin: 0 0 16pt; }
              h2 { font-size: 12pt; margin: 14pt 0 4pt; }
              ol { margin: 0 0 6pt 18pt; }
              li { margin: 0 0 4pt; }
              table { width: 100%; margin-top: 28pt; page-break-inside: avoid; }
              td { width: 50%; padding-top: 30pt; vertical-align: top; }
            </style></head><body>
            <h1>Employment Agreement</h1>
            <p class="sub">between {{employer}} ("the Company") and {{employee}} ("the Employee")</p>
            <h2>1. Appointment</h2>
            <ol>
              <li>The Company employs the Employee as {{role}}, starting on {{start}}.</li>
              <li>The first six months are a probation period. Either party may end the employment during probation with two weeks' written notice.</li>
              <li>The Employee reports to the head of the department named in the offer letter, who may assign other reasonable duties that match the Employee's skills and role.</li>
            </ol>
            <h2>2. Pay and benefits</h2>
            <ol>
              <li>The Company pays a gross salary of INR {{salary}} a year, in twelve equal monthly payments on the last working day of each month, after the deductions the law requires.</li>
              <li>The salary is reviewed once a year. A review does not oblige the Company to raise the salary.</li>
              <li>The Employee may join the Company's health insurance and provident fund schemes on the terms those schemes set from time to time.</li>
            </ol>
            <h2>3. Working time and leave</h2>
            <ol>
              <li>Normal working hours are forty hours a week, Monday to Friday. The Employee may be asked to work reasonable extra hours when the work needs it.</li>
              <li>The Employee has twenty-four working days of paid leave each year, in addition to public holidays. Leave not taken by the end of the year may be carried forward up to ten days.</li>
              <li>Sick leave follows the Company's policy, which allows twelve days a year on full pay with a medical certificate for absences longer than two days.</li>
            </ol>
            <h2>4. Confidentiality</h2>
            <ol>
              <li>The Employee keeps confidential all information about the Company's customers, products, prices, plans, and finances, during the employment and after it ends.</li>
              <li>This duty does not cover information that is already public, other than through a breach of this agreement, or that the law requires the Employee to disclose.</li>
            </ol>
            <h2>5. Intellectual property</h2>
            <ol>
              <li>Work the Employee creates in the course of the employment, including software, documents, and designs, belongs to the Company.</li>
              <li>The Employee signs any document the Company reasonably asks for to record or protect that ownership.</li>
            </ol>
            <h2>6. Ending the employment</h2>
            <ol>
              <li>After probation, either party may end the employment with two months' written notice, or with pay in place of notice.</li>
              <li>The Company may end the employment without notice for serious misconduct, as set out in the Company's disciplinary policy.</li>
              <li>On leaving, the Employee returns all Company property, including equipment, documents, and access cards.</li>
            </ol>
            <h2>7. General</h2>
            <ol>
              <li>This agreement, with the offer letter and the Company's policies, is the whole agreement between the parties about the employment.</li>
              <li>Changes to this agreement are valid only when made in writing and signed by both parties.</li>
              <li>The laws of India govern this agreement, and the courts of Bengaluru have jurisdiction over any dispute about it.</li>
            </ol>
            <table><tr>
              <td>For the Company<br /><br />____________________<br />{{employer}}</td>
              <td>The Employee<br /><br />____________________<br />{{employee}}</td>
            </tr></table>
            </body></html>
            """;
    }

    #endregion

    #endregion
}
