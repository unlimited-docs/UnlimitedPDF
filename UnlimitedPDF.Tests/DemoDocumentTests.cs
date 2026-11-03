using System.Text.RegularExpressions;
using UnlimitedPDF.Demo.ConsoleApp;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demo document places a logo on every page and images in the invoice table, and saves a structurally valid PDF
/// that stores each image once (AC-17, AC-27).
/// </summary>
public class DemoDocumentTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Demo_document_is_valid_and_stores_each_image_once()
    {
        var document = DemoDocument.Build(Fixtures.DemoAssets());
        using var stream = new MemoryStream();
        document.Save(stream);
        var pdf = stream.ToArray();
        var text = Encoding.Latin1.GetString(pdf);

        PdfStructureChecker.AssertValid(pdf);

        // Logo (image and transparency mask), keyboard, dock, and the CMYK sample.
        Assert.Equal(5, Regex.Matches(text, @"/Subtype /Image\b").Count);
        Assert.Contains("/ColorSpace /DeviceCMYK", text);
        Assert.Contains("/Decode [1 0 1 0 1 0 1 0]", text);

        // Every page references the same logo object.
        var logoReferences = Regex.Matches(text, @"/XObject << /Im1 (\d+) 0 R").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(3, logoReferences.Count);
        Assert.Single(logoReferences.Distinct());

        // The invoice stacks each product image above its name.
        Assert.Contains("(Wireless Keyboard) Tj", text);
        Assert.Contains("(USB-C Docking Station) Tj", text);
    }

    #endregion

    #endregion
}
