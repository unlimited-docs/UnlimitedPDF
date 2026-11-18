using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Tests;

/// <summary>
/// A page selection is imported in the order given with repeats kept: links between chosen pages survive across
/// ranges, links and bookmarks to a repeated page target its first copy, and every copy has its own annotations
/// (WR-036, WR-040, AC-01, AC-08, AC-09).
/// </summary>
public class ImportedPageListTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Pages_come_in_the_order_given_with_repeats()
    {
        var output = Import(2, 0, 0, 1);

        Assert.Equal(new[] { "Page 3", "Page 1", "Page 1", "Page 2" }, ImportedPagesTests.PageTexts(output));
        Assert.Equal(4, output.Pages.Select(page => page.ObjectNumber).Distinct().Count());
    }

    [Fact]
    public void A_link_between_chosen_pages_in_different_ranges_is_kept()
    {
        var output = Import(2, 0);

        var link = Annotations(output, 0).Single(a => Subtype(a) == "Link");

        Assert.Equal(output.Pages[1].ObjectNumber, Target(output, link));
    }

    [Fact]
    public void A_link_to_a_page_left_out_is_removed()
    {
        var output = Import(0);

        Assert.DoesNotContain(Annotations(output, 0), a => Subtype(a) == "Link");
        Assert.Contains(Annotations(output, 0), a => Subtype(a) == "Text");
    }

    [Fact]
    public void Links_to_a_repeated_page_target_its_first_copy()
    {
        var output = Import(2, 0, 0);

        var link = Annotations(output, 0).Single(a => Subtype(a) == "Link");

        Assert.Equal(output.Pages[1].ObjectNumber, Target(output, link));
    }

    [Fact]
    public void Every_copy_of_a_repeated_page_has_its_own_annotations()
    {
        var output = Import(0, 1, 0);
        var objects = output.Source.Objects;
        var first = Assert.IsType<PdfReadArray>(objects.Resolve(output.Pages[0].Dictionary["Annots"]));
        var second = Assert.IsType<PdfReadArray>(objects.Resolve(output.Pages[2].Dictionary["Annots"]));

        var firstNumbers = first.Items.OfType<PdfReadReference>().Select(r => r.ObjectNumber).ToList();
        var secondNumbers = second.Items.OfType<PdfReadReference>().Select(r => r.ObjectNumber).ToList();

        Assert.Equal(2, firstNumbers.Count);
        Assert.Equal(2, secondNumbers.Count);
        Assert.Empty(firstNumbers.Intersect(secondNumbers));

        var firstComment = Annotations(output, 0).Single(a => Subtype(a) == "Text");
        var secondComment = Annotations(output, 2).Single(a => Subtype(a) == "Text");
        Assert.Equal(output.Pages[0].ObjectNumber, Assert.IsType<PdfReadReference>(firstComment["P"]).ObjectNumber);
        Assert.Equal(output.Pages[2].ObjectNumber, Assert.IsType<PdfReadReference>(secondComment["P"]).ObjectNumber);

        var firstLink = Annotations(output, 0).Single(a => Subtype(a) == "Link");
        var secondLink = Annotations(output, 2).Single(a => Subtype(a) == "Link");
        Assert.Equal(output.Pages[1].ObjectNumber, Target(output, firstLink));
        Assert.Equal(output.Pages[1].ObjectNumber, Target(output, secondLink));
    }

    [Fact]
    public void A_selection_joins_a_document_as_one_import_with_one_bookmark()
    {
        var report = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var document = new PdfDocument();
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1)), bookmarkTitle: "Cover");
        document.AddPages(report, PdfPageRange.Parse("2,5,7-9"), "Report");
        document.AddPages(PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(2)), bookmarkTitle: "Appendix");
        var pdf = ImportedPagesTests.Save(document);

        PdfStructureChecker.AssertValid(pdf);
        Assert.Equal(new[] { "Page 1", "Page 2", "Page 5", "Page 7", "Page 8", "Page 9", "Page 1", "Page 2" },
            ImportedPagesTests.PageTexts(ImportedPagesTests.Read(pdf)));
        Assert.Equal(new[] { ("Cover", (int?)1), ("Report", (int?)2), ("Appendix", (int?)7) },
            SplitFixtures.Outline(pdf).Select(b => (b.Title, b.TargetPage)));
    }

    [Fact]
    public void A_selection_that_does_not_fit_leaves_the_document_unchanged()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var document = new PdfDocument();
        document.AddPages(file, PdfPageRange.Parse("1-2"));
        var before = ImportedPagesTests.Save(document);

        var pastEnd = Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPages(file, PdfPageRange.Parse("2,5,12")));
        Assert.Throws<ArgumentException>(() => document.AddPages(file, Array.Empty<PdfPageRange>()));
        Assert.Equal("ranges", Assert.Throws<ArgumentNullException>(() => document.AddPages(file, (IEnumerable<PdfPageRange>)null!)).ParamName);
        Assert.Equal("file", Assert.Throws<ArgumentNullException>(() => document.AddPages(null!, PdfPageRange.Parse("1"))).ParamName);

        Assert.Equal("ranges", pastEnd.ParamName);
        Assert.Contains("last page, 10", pastEnd.Message);
        Assert.Equal(before, ImportedPagesTests.Save(document));
    }

    [Fact]
    public void Existing_page_run_calls_still_resolve()
    {
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(10));
        var document = new PdfDocument();

        // AC-14: calls with numbers, named arguments, or only the file keep compiling and behaving the same.
        document.AddPages(file);
        document.AddPages(file, 3, 7);
        document.AddPages(file, lastPage: 2);
        document.AddPages(file, 3, null, "Part 2");

        Assert.Equal(10 + 5 + 2 + 8, ImportedPagesTests.Read(ImportedPagesTests.Save(document)).PageCount);
    }

    [Fact]
    public void Many_repeated_listings_take_time_in_proportion_to_their_count()
    {
        // Review R-04: 8 times the listings must cost about 8 times the time, not 64 times (quadratic).
        var file = PdfFile.FromBytes(ReaderFixtureBuilder.DocumentBytes(1));
        long Time(int count)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            new PdfDocument().AddPages(file, Enumerable.Repeat(new PdfPageRange(1, 1), count));
            return Math.Max(1, watch.ElapsedMilliseconds);
        }

        Time(2_000);
        long small = Time(5_000);
        long large = Time(40_000);

        Assert.True(large < small * 24, $"5,000 listings took {small} ms and 40,000 took {large} ms.");
    }

    [Fact]
    public void Bookmarks_to_a_repeated_page_target_its_first_copy()
    {
        var document = new PdfDocument();
        document.AddPagesCore(PdfFile.FromBytes(SplitFixtures.Bookmarked()), new[] { 7, 4, 4 }, "Pick", trimResources: false);
        var pdf = ImportedPagesTests.Save(document);

        var wrapper = Assert.Single(SplitFixtures.Outline(pdf));
        Assert.Equal("Pick", wrapper.Title);
        Assert.Equal(1, wrapper.TargetPage);

        // Pages 8, 5, 5: Intro and Sales point at pages left out, so Sales' child Europe moves up and targets the
        // first copy of page 5.
        Assert.Equal(new[] { ("Europe", (int?)2), ("Costs", (int?)1) }, wrapper.Children.Select(b => (b.Title, b.TargetPage)));
    }

    [Fact]
    public void Shared_objects_are_copied_once()
    {
        var output = Import(0, 1, 0, 2);

        var fonts = output.Pages
            .Select(page => output.Source.Objects.Resolve(page.Dictionary["Resources"]))
            .OfType<PdfReadDictionary>()
            .Select(resources => output.Source.Objects.Resolve(resources["Font"]))
            .OfType<PdfReadDictionary>()
            .Select(font => Assert.IsType<PdfReadReference>(font["F1"]).ObjectNumber)
            .Distinct();

        Assert.Single(fonts);
    }

    #endregion

    #region Private Methods

    private static List<PdfReadDictionary> Annotations(PdfFile output, int page) =>
        output.Source.Objects.Resolve(output.Pages[page].Dictionary["Annots"]) is PdfReadArray array
            ? array.Items.Select(item => Assert.IsType<PdfReadDictionary>(output.Source.Objects.Resolve(item))).ToList()
            : new List<PdfReadDictionary>();

    // Three pages sharing one font. Page 1 links to page 2 and holds a comment; page 3 links to page 1.
    private static byte[] Fixture()
    {
        const string Rect = "/Rect [0 0 10 10]";
        return new ReaderFixtureBuilder()
            .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .AddObject(2, "<< /Type /Pages /Kids [4 0 R 6 0 R 8 0 R] /Count 3 /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> >>")
            .AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .AddObject(4, "<< /Type /Page /Parent 2 0 R /Contents 5 0 R /Annots [10 0 R 11 0 R] >>")
            .AddStream(5, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 1) Tj ET"))
            .AddObject(6, "<< /Type /Page /Parent 2 0 R /Contents 7 0 R >>")
            .AddStream(7, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 2) Tj ET"))
            .AddObject(8, "<< /Type /Page /Parent 2 0 R /Contents 9 0 R /Annots [12 0 R] >>")
            .AddStream(9, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Page 3) Tj ET"))
            .AddObject(10, $"<< /Type /Annot /Subtype /Link {Rect} /Dest [6 0 R /Fit] /P 4 0 R >>")
            .AddObject(11, $"<< /Type /Annot /Subtype /Text {Rect} /Contents (Note) /P 4 0 R >>")
            .AddObject(12, $"<< /Type /Annot /Subtype /Link {Rect} /Dest [4 0 R /Fit] /P 8 0 R >>")
            .ToBytes();
    }

    private static PdfFile Import(params int[] pageIndexes)
    {
        var document = new PdfDocument();
        document.AddPagesCore(PdfFile.FromBytes(Fixture()), pageIndexes, null, trimResources: false);
        var pdf = ImportedPagesTests.Save(document);
        PdfStructureChecker.AssertValid(pdf);
        return ImportedPagesTests.Read(pdf);
    }

    private static string? Subtype(PdfReadDictionary annotation) => (annotation["Subtype"] as PdfReadName)?.Value;

    // The output page object a link targets.
    private static int Target(PdfFile output, PdfReadDictionary link)
    {
        var destination = Assert.IsType<PdfReadArray>(output.Source.Objects.Resolve(link["Dest"]));
        return Assert.IsType<PdfReadReference>(destination[0]).ObjectNumber;
    }

    #endregion

    #endregion
}
