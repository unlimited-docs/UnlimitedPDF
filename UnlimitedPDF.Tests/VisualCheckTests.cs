using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The visual-check PDF that testers open on each viewer target is structurally valid. Set
/// <c>UNLIMITEDPDF_VISUAL_CHECK_DIR</c> to also write it to that folder (AC-18, AC-27; WR-025).
/// </summary>
public class VisualCheckTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Visual_check_document_is_valid_and_written_on_request()
    {
        using var stream = new MemoryStream();
        VisualCheckDocument.Build().Save(stream);
        var pdf = stream.ToArray();

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(3, Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count);

        var directory = Environment.GetEnvironmentVariable(VisualCheckDocument.OutputVariable);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "visual-check.pdf"), pdf);
        }
    }

    #endregion

    #endregion
}
