using System.Text.RegularExpressions;
using UnlimitedPDF.Demo.ConsoleApp;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demo document places a logo in the header of every page and images in the invoice table, and saves a
/// structurally valid PDF that stores each image once.
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

        // Every page references the same logo object from its header band.
        var pageImages = Regex.Matches(text, @"/XObject << ([^>]*) >>")
            .Select(m => Regex.Matches(m.Groups[1].Value, @"(\d+) 0 R").Select(r => r.Groups[1].Value).ToHashSet())
            .ToList();
        Assert.Equal(3, pageImages.Count);
        Assert.Single(pageImages.Aggregate((shared, next) => shared.Intersect(next).ToHashSet()));

        // The invoice stacks each product image above its name.
        Assert.Contains("(Wireless Keyboard) Tj", text);
        Assert.Contains("(USB-C Docking Station) Tj", text);
    }

    [Fact]
    public void Demo_document_splits_into_one_valid_file_per_page()
    {
        // The console demo's split call (WR-037, AC-14): the saved sample, one page per part, into a folder.
        string root = Path.Combine(Path.GetTempPath(), $"demo-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string sample = Path.Combine(root, "unlimited-pdf-sample.pdf");
            DemoDocument.Build(Fixtures.DemoAssets()).Save(sample);

            var parts = PdfOperations.Split(PdfFile.FromFile(sample), 1, Path.Combine(root, "unlimited-pdf-sample-split"));

            Assert.Equal(new[] { "unlimited-pdf-sample-1.pdf", "unlimited-pdf-sample-2.pdf", "unlimited-pdf-sample-3.pdf" }, parts.Select(p => p.FileName));
            Assert.All(parts, part => PdfStructureChecker.AssertValid(File.ReadAllBytes(Path.Combine(root, "unlimited-pdf-sample-split", part.FileName))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    #endregion

    #endregion
}
