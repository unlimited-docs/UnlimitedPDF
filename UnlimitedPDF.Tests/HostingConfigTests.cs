using System.Runtime.CompilerServices;
using System.Text.Json;
using UnlimitedPDF.Demos.BlazorWasm.Build;
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

        // The whole policy, directive by directive: nothing broader may slip in.
        Assert.Equal(
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-IMPORTMAP-HASH'; style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; "
            + "form-action 'self'; frame-ancestors 'none'",
            csp);
        Assert.Equal("nosniff", headers.GetProperty("X-Content-Type-Options").GetString());
        Assert.Equal("strict-origin-when-cross-origin", headers.GetProperty("Referrer-Policy").GetString());
        Assert.Equal("DENY", headers.GetProperty("X-Frame-Options").GetString());
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=(), usb=()", headers.GetProperty("Permissions-Policy").GetString());
        Assert.Equal(5, headers.EnumerateObject().Count());
    }

    [Fact]
    public void Unknown_addresses_open_the_app_and_missing_files_stay_missing()
    {
        var config = Config();
        var fallback = config.GetProperty("navigationFallback");

        Assert.Equal("never", config.GetProperty("trailingSlash").GetString());
        Assert.Equal("/index.html", fallback.GetProperty("rewrite").GetString());
        string files = fallback.GetProperty("exclude").EnumerateArray().Last().GetString()!;
        Assert.All(["wasm", "br", "js", "css", "map", "md", "pdf", "woff2", "ttf", "webmanifest"], extension => Assert.Contains("," + extension, "," + files.Trim('/', '*', '.', '{', '}'), StringComparison.Ordinal));
    }

    [Fact]
    public void The_import_map_hash_is_the_browsers_and_index_html_has_no_other_inline_script()
    {
        const string map = "{\n  \"imports\": {\n    \"./dotnet.js\": \"./dotnet.abc.js\"\n  }\n}";
        string lf = $"<!DOCTYPE html><html><head><script type=\"importmap\">{map}</script><script src=\"app.js\"></script></head></html>";
        string crlf = lf.Replace("\n", "\r\n");
        string expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(map)));

        // The browser hashes the text as parsed, where every line break is one line feed.
        Assert.Equal(expected, ImportMapHash.Compute(lf));
        Assert.Equal(expected, ImportMapHash.Compute(crlf));
        Assert.Equal(expected, ImportMapHash.Compute("\uFEFF" + lf));
        Assert.Equal("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=", ImportMapHash.Compute("<script type=\"importmap\"></script>"));

        // No import map, or a second inline script, stops the publish: the policy would block it on the host.
        Assert.Throws<InvalidOperationException>(() => ImportMapHash.Compute("<script src=\"app.js\"></script>"));
        Assert.Throws<InvalidOperationException>(() => ImportMapHash.Compute(lf + "<script type=\"module\">go()</script>"));
        Assert.Throws<InvalidOperationException>(() => ImportMapHash.Compute(lf.Replace("importmap", "module")));

        // The source page has exactly the one import map (the build fills it in), so the step accepts it.
        _ = ImportMapHash.Compute(DemosFile("wwwroot/index.html"));
    }

    [Fact]
    public void The_hash_is_written_from_the_source_template_so_publishing_again_works()
    {
        string template = DemosFile("wwwroot/staticwebapp.config.json");

        string first = ImportMapHash.Apply(template, "AAA=");
        string second = ImportMapHash.Apply(template, "BBB=");

        Assert.Contains("'sha256-AAA='", first, StringComparison.Ordinal);
        Assert.Contains("'sha256-BBB='", second, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ImportMapHash.Apply(first, "CCC="));

        string project = DemosFile("UnlimitedPDF.Demos.BlazorWasm.csproj");
        Assert.Contains("<Code Type=\"Class\" Language=\"cs\" Source=\"$(MSBuildThisFileDirectory)Build/WriteImportMapHash.cs\" />", project, StringComparison.Ordinal);
        Assert.Contains("<WriteImportMapHash Folder=\"$(PublishDir)wwwroot\" Template=\"$(MSBuildThisFileDirectory)wwwroot/staticwebapp.config.json\" />", project, StringComparison.Ordinal);
        Assert.Contains("<Compile Remove=\"Build\\**\" />", project, StringComparison.Ordinal);
    }

    [Fact]
    public void The_publish_step_paths_work_on_linux()
    {
        // The site is published on a Linux runner, where a backslash is part of a file name. The task's source file and
        // its template reach file reads as plain strings, so they must use forward slashes (QF-26111801).
        var project = System.Xml.Linq.XDocument.Parse(DemosFile("UnlimitedPDF.Demos.BlazorWasm.csproj"));
        string[] paths =
        [
            project.Descendants("Code").Single().Attribute("Source")!.Value,
            project.Descendants("WriteImportMapHash").Single().Attribute("Template")!.Value,
            project.Descendants("WriteImportMapHash").Single().Attribute("Folder")!.Value,
        ];

        Assert.All(paths, path => Assert.DoesNotContain('\\', path));
    }

    #endregion

    #region Private Methods

    private static JsonElement Config() => JsonDocument.Parse(DemosFile("wwwroot/staticwebapp.config.json")).RootElement;

    private static string DemosFile(string relative, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    #endregion

    #endregion
}
