using UnlimitedPDF;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Generates the merge demos' built-in files: other demos' documents with their default inputs, saved and loaded back
/// as existing PDFs, so the merge demos work before anything is uploaded and no PDF files are shipped.
/// </summary>
public static class MergeDemoFiles
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the Account statement demo's document.
    /// </summary>
    public static async Task<PdfFile> AccountStatementAsync(SampleImageStore images) =>
        Load(AccountStatementSample.Build(new AccountStatementInput(), await images.GetManyAsync("account-statement-logo.png")));

    /// <summary>
    /// Returns the Certificate demo's document.
    /// </summary>
    public static async Task<PdfFile> CertificateAsync(SampleImageStore images) =>
        Load(CertificateSample.Build(new CertificateInput(), await images.GetManyAsync("certificate-seal.png", "certificate-signature.png")));

    /// <summary>
    /// Returns the Employee directory demo's document (several pages).
    /// </summary>
    public static PdfFile EmployeeDirectory() => Load(EmployeeDirectorySample.Build(new EmployeeDirectoryInput()));

    /// <summary>
    /// Returns the Invoice demo's document.
    /// </summary>
    public static async Task<PdfFile> InvoiceAsync(SampleImageStore images)
    {
        var input = new InvoiceInput();
        var photos = input.Items.Select(item => item.Photo).Where(photo => photo.Length > 0);
        return Load(InvoiceSample.Build(input, await images.GetManyAsync(["invoice-logo.png", .. photos])));
    }

    /// <summary>
    /// Loads a built document back as an existing PDF.
    /// </summary>
    public static PdfFile Load(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream);
        return PdfFile.FromBytes(stream.ToArray());
    }

    /// <summary>
    /// Returns the Medical report demo's document.
    /// </summary>
    public static async Task<PdfFile> MedicalReportAsync(SampleImageStore images) =>
        Load(MedicalReportSample.Build(new MedicalReportInput(), await images.GetManyAsync("medical-report-logo.png")));

    /// <summary>
    /// Returns the Payslip demo's document.
    /// </summary>
    public static async Task<PdfFile> PayslipAsync(SampleImageStore images) =>
        Load(PayslipSample.Build(new PayslipInput(), await images.GetManyAsync("payslip-logo.png")));

    /// <summary>
    /// Returns the Quotation demo's document.
    /// </summary>
    public static async Task<PdfFile> QuotationAsync(SampleImageStore images) =>
        Load(QuotationSample.Build(new QuotationInput(), await images.GetManyAsync("quotation-logo.png")));

    /// <summary>
    /// Returns the Receipt demo's document.
    /// </summary>
    public static async Task<PdfFile> ReceiptAsync(SampleImageStore images) =>
        Load(ReceiptSample.Build(new ReceiptInput(), await images.GetManyAsync("receipt-logo.png")));

    /// <summary>
    /// Returns the Tables demo's document.
    /// </summary>
    public static PdfFile Tables() => Load(TablesSample.Build(new TablesInput()));

    /// <summary>
    /// Returns the Text and pages demo's document.
    /// </summary>
    public static PdfFile TextAndPages() => Load(TextBasicsSample.Build(new TextBasicsInput()));

    #endregion

    #endregion
}
