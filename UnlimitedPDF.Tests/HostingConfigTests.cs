using System.Runtime.CompilerServices;
using System.Text.Json;
using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The demos site's hosting config, <c>wwwroot/staticwebapp.config.json</c> (26111701 AC-15, D-017, T-14): every
/// address the site had before the redesign still opens a page, directly or by a permanent redirect to its new home;
/// and every response carries the security headers, with a Content-Security-Policy that allows only the site's own
/// scripts, the WebAssembly runtime, and the one inline import map by its hash (OWASP A02).
/// </summary>
public class HostingConfigTests
{
    #region Fields and Constants

    // The pages of the site at the start of the redesign (the sitemap at 7854bf9).
    private static readonly string[] EarlierAddresses =
    [
        "", "faq", "playground/create/text-and-pages", "playground/create/tables", "playground/create/images",
        "playground/create/headers-and-footers", "playground/merge/join-files", "playground/merge/cover-and-ranges",
        "playground/merge/pick-pages", "playground/merge/bookmarks-and-numbers", "playground/merge/month-end-pack",
        "playground/merge/employee-pack", "playground/merge/patient-pack", "playground/split/every-n-pages",
        "playground/split/page-ranges", "playground/html/html-to-pdf", "playground/html/common-problems",
        "playground/languages/fonts-and-languages", "playground/documents/invoice", "playground/documents/quotation",
        "playground/documents/purchase-order", "playground/documents/delivery-note", "playground/documents/account-statement",
        "playground/documents/payslip", "playground/documents/medical-report", "playground/documents/receipt",
        "playground/documents/certificate", "playground/documents/employee-directory",
    ];

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Every_earlier_address_opens_a_page_or_moves_permanently_to_its_new_home()
    {
        var redirects = Config().GetProperty("routes").EnumerateArray()
            .Where(r => r.TryGetProperty("redirect", out _))
            .ToDictionary(r => r.GetProperty("route").GetString()!, r => (Target: r.GetProperty("redirect").GetString()!, Status: r.GetProperty("statusCode").GetInt32()));
        var pages = SampleCatalog.Pages.Select(d => d.Route).Concat(["", "faq", ScenarioGallery.Route]).ToHashSet(StringComparer.Ordinal);

        foreach (string address in EarlierAddresses)
        {
            if (pages.Contains(address))
            {
                Assert.False(redirects.ContainsKey("/" + address), $"/{address} is a page and must not redirect");
                continue;
            }

            Assert.True(redirects.TryGetValue("/" + address, out var move), $"/{address} has no redirect");
            Assert.Equal(301, move.Status);
            string target = move.Target.TrimStart('/');
            bool gallery = target.StartsWith(ScenarioGallery.Route + "?", StringComparison.Ordinal)
                && ScenarioGallery.FindCapability(target.Split("capability=")[1].Split('&')[0]) is not null;
            bool variant = DemoVariants.InvoiceTypes.Concat(DemoVariants.PackDomains).Any(v => v.Address == target);
            Assert.True(pages.Contains(target) || gallery || variant, $"/{address} moves to /{target}, which is not a page");
        }

        // Every redirect is from an earlier address; none is left over.
        Assert.All(redirects.Keys, route => Assert.Contains(route.TrimStart('/'), EarlierAddresses));
    }

    [Fact]
    public void Every_response_carries_the_security_headers()
    {
        var headers = Config().GetProperty("globalHeaders");
        string csp = headers.GetProperty("Content-Security-Policy").GetString()!;
        var directives = csp.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(d => d.Split(' ')[0], d => d.Split(' ').Skip(1).ToArray());

        Assert.Equal(["'self'", "'wasm-unsafe-eval'", "'sha256-IMPORTMAP-HASH'"], directives["script-src"]);
        Assert.Equal(["'self'"], directives["default-src"]);
        Assert.Equal(["'self'"], directives["connect-src"]);
        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'none'"], directives["frame-ancestors"]);
        Assert.Equal(["'self'"], directives["base-uri"]);
        Assert.DoesNotContain("'unsafe-eval'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", headers.GetProperty("X-Content-Type-Options").GetString());
        Assert.Equal("strict-origin-when-cross-origin", headers.GetProperty("Referrer-Policy").GetString());
        Assert.Equal("DENY", headers.GetProperty("X-Frame-Options").GetString());
        Assert.False(string.IsNullOrEmpty(headers.GetProperty("Permissions-Policy").GetString()));
    }

    [Fact]
    public void The_published_config_gets_the_hash_of_the_import_map()
    {
        // The browser hashes the import map as parsed, with line feeds only; the publish step does the same.
        string project = DemosFile("UnlimitedPDF.Demos.BlazorWasm.csproj");

        Assert.Contains("<Target Name=\"WriteImportMapHashAfterPublish\" AfterTargets=\"Publish\">", project, StringComparison.Ordinal);
        Assert.Contains("<WriteImportMapHash Folder=\"$(PublishDir)wwwroot\" />", project, StringComparison.Ordinal);
        Assert.Contains("Replace(\"\\r\\n\", \"\\n\").Replace('\\r', '\\n')", project, StringComparison.Ordinal);
        Assert.Contains("config.Replace(\"IMPORTMAP-HASH\", hash)", project, StringComparison.Ordinal);
        Assert.Contains("<script type=\"importmap\"></script>", DemosFile("wwwroot/index.html"), StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", DemosFile("wwwroot/index.html"), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static JsonElement Config() => JsonDocument.Parse(DemosFile("wwwroot/staticwebapp.config.json")).RootElement;

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    #endregion

    #endregion
}
