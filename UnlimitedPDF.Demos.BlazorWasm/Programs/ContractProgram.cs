// Contract: an employment agreement written in HTML, with numbered clauses and page X of Y.
// A complete console program: it writes contract.pdf.
//
// Run it:
//   1. dotnet new console -n ContractExample
//   2. cd ContractExample
//   3. dotnet add package UnlimitedPDF
//   4. Replace everything in Program.cs with this code.
//   5. dotnet run
// Then open contract.pdf in the ContractExample folder.

using UnlimitedPDF;

namespace ContractExample;

public static class Program
{
    public static void Main()
    {
        // The sample data: change it, run again, and open contract.pdf.
        string employerName = "Northwind Software Pvt. Ltd.";
        string employeeName = "Priya Sharma";
        string role = "Senior Software Engineer";
        DateTime startDate = new(2026, 12, 1);
        decimal annualSalary = 1_800_000m;

        string contract = Html(employerName, employeeName, role, startDate, annualSalary);
        var html = PdfHtml.FromString(contract);

        // Long paragraphs wrap across pages; the signature table is kept together; the footer
        // shows "Page X of Y" on every page.
        var document = new PdfDocument
        {
            MarginTop = 48, MarginBottom = 40, MarginLeft = 60, MarginRight = 60,
        };
        document.Footer.Height = 20;
        document.Footer.AddText("Employment agreement", 0, 6, 8);
        document.Footer.AddText("Page {page} of {pages}", 0, 6, 8, PdfTextAlignment.Right);
        document.Body.AddHtml(html);

        document.Save("contract.pdf");
        Console.WriteLine($"Wrote {Path.GetFullPath("contract.pdf")}");
    }

    // Writes the contract's HTML. The values are encoded before they go into the HTML.
    private static string Html(
        string employerName, string employeeName, string role, DateTime startDate,
        decimal annualSalary)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        string employer = System.Net.WebUtility.HtmlEncode(employerName);
        string employee = System.Net.WebUtility.HtmlEncode(employeeName);
        string title = System.Net.WebUtility.HtmlEncode(role);
        string start = startDate.ToString("d MMMM yyyy", invariant);
        string salary = annualSalary.ToString("N0", invariant);

        return $$"""
            <html><head><style>
              body {
                font-family: Helvetica, sans-serif; font-size: 10.5pt; line-height: 1.45;
                color: #1a1a1a;
              }
              h1 { font-size: 18pt; text-align: center; margin: 0 0 4pt; }
              .sub { text-align: center; color: #555; margin: 0 0 16pt; }
              h2 { font-size: 12pt; margin: 14pt 0 4pt; }
              ol { margin: 0 0 6pt 18pt; }
              li { margin: 0 0 4pt; }
              table { width: 100%; margin-top: 28pt; page-break-inside: avoid; }
              td { width: 50%; padding-top: 30pt; vertical-align: top; }
            </style></head><body>
            <h1>Employment Agreement</h1>
            <p class="sub">between {{employer}} ("the Company") and
              {{employee}} ("the Employee")</p>
            <h2>1. Appointment</h2>
            <ol>
              <li>The Company employs the Employee as {{title}}, starting on {{start}}.</li>
              <li>The first six months are a probation period. Either party may end the employment
                during probation with two weeks' written notice.</li>
              <li>The Employee reports to the head of the department named in the offer letter, who
                may assign other reasonable duties that match the Employee's skills and role.</li>
            </ol>
            <h2>2. Pay and benefits</h2>
            <ol>
              <li>The Company pays a gross salary of INR {{salary}} a year, in twelve equal monthly
                payments on the last working day of each month, after the deductions the law
                requires.</li>
              <li>The salary is reviewed once a year. A review does not oblige the Company to raise
                the salary.</li>
              <li>The Employee may join the Company's health insurance and provident fund schemes on
                the terms those schemes set from time to time.</li>
            </ol>
            <h2>3. Working time and leave</h2>
            <ol>
              <li>Normal working hours are forty hours a week, Monday to Friday. The Employee may be
                asked to work reasonable extra hours when the work needs it.</li>
              <li>The Employee has twenty-four working days of paid leave each year, in addition to
                public holidays. Leave not taken by the end of the year may be carried forward up
                to ten days.</li>
              <li>Sick leave follows the Company's policy, which allows twelve days a year on full
                pay with a medical certificate for absences longer than two days.</li>
            </ol>
            <h2>4. Confidentiality</h2>
            <ol>
              <li>The Employee keeps confidential all information about the Company's customers,
                products, prices, plans, and finances, during the employment and after it
                ends.</li>
              <li>This duty does not cover information that is already public, other than through a
                breach of this agreement, or that the law requires the Employee to disclose.</li>
            </ol>
            <h2>5. Intellectual property</h2>
            <ol>
              <li>Work the Employee creates in the course of the employment, including software,
                documents, and designs, belongs to the Company.</li>
              <li>The Employee signs any document the Company reasonably asks for to record or
                protect that ownership.</li>
            </ol>
            <h2>6. Ending the employment</h2>
            <ol>
              <li>After probation, either party may end the employment with two months' written
                notice, or with pay in place of notice.</li>
              <li>The Company may end the employment without notice for serious misconduct, as set
                out in the Company's disciplinary policy.</li>
              <li>On leaving, the Employee returns all Company property, including equipment,
                documents, and access cards.</li>
            </ol>
            <h2>7. General</h2>
            <ol>
              <li>This agreement, with the offer letter and the Company's policies, is the whole
                agreement between the parties about the employment.</li>
              <li>Changes to this agreement are valid only when made in writing and signed by both
                parties.</li>
              <li>The laws of India govern this agreement, and the courts of Bengaluru have
                jurisdiction over any dispute about it.</li>
            </ol>
            <table><tr>
              <td>For the Company<br /><br />____________________<br />{{employer}}</td>
              <td>The Employee<br /><br />____________________<br />{{employee}}</td>
            </tr></table>
            </body></html>
            """;
    }
}
