using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static UnlimitedPDF.Tests.Html.HtmlLayoutTests;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// Images, links, heading bookmarks, the one-call conversion, and the platform boundary of HTML to PDF (WR-038, PR-018,
/// AC-02, AC-04, AC-14, AC-15, AC-18, AC-22, D-005).
/// </summary>
public class HtmlEntryPointTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Images_draw_at_96_pixels_per_inch_and_keep_their_aspect_ratio()
    {
        string png = Data(PdfHtmlLoadingTests.Png(40));
        var page = Assert.Single(Render($"<img src='{png}'><img src='{png}' width=100><img src='{png}' style='height:15pt'>"));
        var draws = Draws(page);
        Assert.Equal(3, draws.Count);
        Assert.Equal((30d, 30d), (draws[0].Width, draws[0].Height));
        Assert.Equal((75d, 75d), (draws[1].Width, draws[1].Height));
        Assert.Equal((15d, 15d), (draws[2].Width, draws[2].Height));
        Assert.Contains("/XObject << /Im1 ", Encoding.ASCII.GetString(Save($"<img src='{png}'>")));
    }

    [Fact]
    public void Images_wider_than_the_line_or_taller_than_the_body_scale_down()
    {
        string wide = Data(new PngBuilder { ColorType = 6, Width = 2000, Height = 100 }.Build());
        string tall = Data(new PngBuilder { ColorType = 6, Width = 100, Height = 4000 }.Build());
        var pages = Render($"<img src='{wide}'><br><img src='{tall}'>", bodyMargin: false);
        var draws = pages.SelectMany(Draws).ToList();
        Assert.Equal(Right - Left, draws[0].Width, 2);
        Assert.Equal((Right - Left) / 20, draws[0].Height, 2);
        Assert.True(draws[1].Height <= Top - Bottom + 0.01);
        Assert.Equal(draws[1].Height / 40, draws[1].Width, 2);
    }

    [Fact]
    public void Images_go_in_table_cells_list_items_and_blocks_and_failed_ones_show_their_alt_text()
    {
        string png = Data(PdfHtmlLoadingTests.Png(8));
        var page = Assert.Single(Render($"<table border=1><tr><td><img src='{png}'><td>x</table><ul><li><img src='{png}'> item</ul><img src='{png}' style='display:block; width:50px'><img src='missing.png' alt='Company logo'>"));
        Assert.Equal(3, Draws(page).Count);
        Assert.Contains(Runs(page), r => r.Text == "Company logo");
        Assert.Contains(Runs(page), r => r.Text == "1." || r.Text == "item" || r.Text == " item");
    }

    [Fact]
    public void Alpha_png_images_keep_their_transparency()
    {
        string pdf = Encoding.ASCII.GetString(Save($"<img src='{Data(PdfHtmlLoadingTests.Png(6))}'>"));
        Assert.Contains("/SMask", pdf);
    }

    [Fact]
    public void Web_and_mail_links_become_uri_annotations_and_other_schemes_are_dropped()
    {
        var html = PdfHtml.FromString("<p><a href='https://acme.com/a b'>acme</a> <a href='mailto:sales@acme.com'>mail</a> <a href='javascript:alert(1)'>x</a> <a href='https://acme.com/é'>accent</a></p>");
        string pdf = Encoding.ASCII.GetString(SaveHtml(html));
        Assert.Contains("/A << /S /URI /URI (https://acme.com/a%20b) >>", pdf);
        Assert.Contains("/URI (mailto:sales@acme.com)", pdf);
        Assert.Contains("/URI (https://acme.com/%C3%A9)", pdf);
        Assert.DoesNotContain("javascript", pdf);
        Assert.Equal(3, Regex.Matches(pdf, "/Subtype /Link").Count);
        var warning = Assert.Single(html.Warnings);
        Assert.Equal(PdfHtmlWarningKind.LinkDropped, warning.Kind);
        Assert.Equal(1, warning.PageNumber);
        Assert.Contains("(x) Tj", pdf);
    }

    [Fact]
    public void Id_links_jump_to_their_target_on_a_later_page()
    {
        string filler = string.Concat(Enumerable.Range(1, 80).Select(i => $"<p>p{i}</p>"));
        string pdf = Encoding.ASCII.GetString(Save($"<p><a href='#costs'>see costs</a> <a href='#nowhere'>none</a></p>{filler}<h2 id=costs>Costs</h2>"));
        var dest = Regex.Match(pdf, @"/Dest \[(\d+) 0 R /XYZ [\d.]+ [\d.]+ null\]");
        Assert.True(dest.Success);
        var kids = Regex.Match(pdf, @"/Kids \[([^\]]*)\]").Groups[1].Value.Split(new[] { " 0 R" }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();
        Assert.True(kids.IndexOf(dest.Groups[1].Value) > 0);
        Assert.Single(Regex.Matches(pdf, "/Subtype /Link"));
    }

    [Fact]
    public void A_link_wrapping_onto_two_lines_has_one_rectangle_per_line()
    {
        string words = string.Join(" ", Enumerable.Range(1, 40).Select(i => "linked" + i));
        string pdf = Encoding.ASCII.GetString(Save($"<p>start <a href='https://example.com'>{words}</a></p>"));
        Assert.True(Regex.Matches(pdf, "/Subtype /Link").Count >= 2);
    }

    [Fact]
    public void Heading_bookmarks_are_off_by_default_and_nest_by_level_when_on()
    {
        const string html = "<h1>Summary</h1><h2>Costs</h2><h3>Detail</h3><h2>Revenue</h2><h1>Appendix</h1>";
        Assert.DoesNotContain("/Outlines", Encoding.ASCII.GetString(Save(html)));

        var doc = Document();
        doc.Body.AddHtml(PdfHtml.FromString(html), headingBookmarks: true);
        string pdf = Encoding.ASCII.GetString(PdfPageReader.SaveBytes(doc));
        Assert.Contains("/Type /Outlines", pdf);
        Assert.Contains("/Count 5", pdf);
        foreach (var title in new[] { "Summary", "Costs", "Detail", "Revenue", "Appendix" })
            Assert.Contains("/Title <" + string.Concat(Encoding.ASCII.GetBytes(title).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + ">", pdf);
    }

    [Fact]
    public void Heading_bookmarks_join_imported_bookmarks_in_page_order()
    {
        var first = Document();
        first.Body.AddHtml(PdfHtml.FromString("<h1>Imported</h1>"), headingBookmarks: true);
        var file = PdfFile.FromBytes(PdfPageReader.SaveBytes(first));

        var doc = Document();
        doc.AddPages(file, bookmarkTitle: "Attachment");
        doc.Body.AddHtml(PdfHtml.FromString("<h1>Own</h1>"), headingBookmarks: true);
        var pdf = PdfPageReader.SaveBytes(doc);
        PdfStructureChecker.AssertValid(pdf);
        string text = Encoding.ASCII.GetString(pdf);
        int attachment = text.IndexOf(Hex("Attachment"), StringComparison.Ordinal);
        int own = text.IndexOf(Hex("Own"), StringComparison.Ordinal);
        Assert.True(attachment > 0 && own > attachment);
        Assert.Contains(Hex("Imported"), text);
    }

    [Fact]
    public async Task One_call_conversion_writes_the_same_bytes_as_the_manual_document()
    {
        var html = PdfHtml.FromString("<h1>Report</h1><p>Body text</p>");
        var manual = new PdfDocument(PdfPageSize.Letter) { MarginTop = 72, MarginBottom = 36, MarginLeft = 36, MarginRight = 36 };
        manual.Body.AddHtml(html, headingBookmarks: true);
        byte[] expected = PdfPageReader.SaveBytes(manual);

        var options = new PdfHtmlConvertOptions { PageSize = PdfPageSize.Letter, MarginTop = 72, HeadingBookmarks = true };
        using var stream = new MemoryStream();
        PdfOperations.ConvertHtml(html, stream, options);
        Assert.Equal(expected, stream.ToArray());

        using var asyncStream = new MemoryStream();
        await PdfOperations.ConvertHtmlAsync(html, asyncStream, options);
        Assert.Equal(expected, asyncStream.ToArray());

        string path = Path.Combine(Path.GetTempPath(), "updf-convert-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            PdfOperations.ConvertHtml(html, path, options);
            Assert.Equal(expected, File.ReadAllBytes(path));
            await PdfOperations.ConvertHtmlAsync(html, path, options);
            Assert.Equal(expected, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void One_call_conversion_defaults_to_A4_with_half_inch_margins()
    {
        using var stream = new MemoryStream();
        PdfOperations.ConvertHtml(PdfHtml.FromString("<body style='margin:0'><p style='margin:0'>x</p>"), stream);
        var page = Assert.Single(PdfPageReader.Read(stream.ToArray()));
        Assert.Equal("[0 0 595.28 841.89]", page.MediaBox);
        Assert.Equal(36, Runs(page).Single().X, 3);
    }

    [Fact]
    public void Entry_points_check_their_arguments()
    {
        var html = PdfHtml.FromString("<p>x</p>");
        Assert.Equal("html", Assert.Throws<ArgumentNullException>(() => PdfOperations.ConvertHtml(null!, new MemoryStream())).ParamName);
        Assert.Equal("output", Assert.Throws<ArgumentNullException>(() => PdfOperations.ConvertHtml(html, (Stream)null!)).ParamName);
        Assert.Equal("outputPath", Assert.Throws<ArgumentNullException>(() => PdfOperations.ConvertHtml(html, (string)null!)).ParamName);
        Assert.Equal("html", Assert.Throws<ArgumentNullException>(() => new PdfDocument().Body.AddHtml(null!)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocument().Body.AddHtml(html, spacing: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlConvertOptions { MarginLeft = double.NaN });
    }

    [Fact]
    public async Task Cancellation_stops_an_async_conversion()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.ConvertHtmlAsync(PdfHtml.FromString("<p>x</p>"), new MemoryStream(), null, cancelled.Token));
    }

    [Fact]
    public void Scripts_never_run_and_the_library_has_no_network_or_new_package_dependency()
    {
        Assert.DoesNotContain("PAID", Encoding.ASCII.GetString(Save("<p>Invoice</p><script>document.write('PAID')</script>")));

        var references = typeof(PdfHtml).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain(references, name => name is not null && (name.StartsWith("System.Net", StringComparison.Ordinal) || name.Contains("Http")));

        string csproj = File.ReadAllText(RepositoryFile(Path.Combine("UnlimitedPDF", "UnlimitedPDF.csproj")));
        var packages = Regex.Matches(csproj, "PackageReference Include=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct();
        Assert.Equal(new[] { "Microsoft.AspNetCore.Components.Web" }, packages);
    }

    [Fact]
    public void The_entry_points_have_the_approved_parameters_and_metadata()
    {
        var addHtml = typeof(PdfBodyFlow).GetMethod(nameof(PdfBodyFlow.AddHtml))!;
        Assert.Equal("html,spacing,headingBookmarks,keepWithNext", string.Join(",", addHtml.GetParameters().Select(p => p.Name)));
        var methods = typeof(PdfOperations).GetMethods().Where(m => m.Name.StartsWith("ConvertHtml", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, methods.Count);
        foreach (var method in methods.Append(addHtml))
        {
            Assert.Equal("1.0.0", method.GetCustomAttribute<AddedVersionAttribute>()!.Version);
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()!.Description));
        }
        Assert.Equal(new[] { "HeadingBookmarks", "MarginBottom", "MarginLeft", "MarginRight", "MarginTop", "PageSize" },
            typeof(PdfHtmlConvertOptions).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("UnlimitedPDF", typeof(PdfHtmlConvertOptions).Namespace);
    }

    #endregion

    #region Internal Methods

    internal static string Data(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);

    internal static List<(double Width, double Height, double X, double Y)> Draws(PdfPageReader.Page page) =>
        Regex.Matches(page.Content, @"q ([\d.]+) 0 0 ([\d.]+) ([\d.]+) ([\d.]+) cm /Im\d+ Do Q").Cast<Match>()
            .Select(m => (D(m.Groups[1].Value), D(m.Groups[2].Value), D(m.Groups[3].Value), D(m.Groups[4].Value))).ToList();

    #endregion

    #region Private Methods

    private static double D(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private static string Hex(string title) => "<" + string.Concat(Encoding.ASCII.GetBytes(title).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + ">";

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!)!, relative);

    private static byte[] Save(string html) => SaveHtml(PdfHtml.FromString(html));

    private static byte[] SaveHtml(PdfHtml html)
    {
        var doc = Document();
        doc.Body.AddHtml(html);
        var pdf = PdfPageReader.SaveBytes(doc);
        PdfStructureChecker.AssertValid(pdf);
        return pdf;
    }

    #endregion

    #endregion
}
