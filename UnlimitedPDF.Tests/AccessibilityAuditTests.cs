using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The fixes of the WCAG 2.2 AA audit (26111701 T-16, V-13), kept from coming back: every field has a name, focus is
/// always visible and never under the top bar, nothing scrolls sideways at 320 px, states are more than colour, errors
/// read as sentences, text in other languages says so, and status changes are announced.
/// </summary>
public class AccessibilityAuditTests
{
    #region Methods

    #region Public Methods

    [Fact]
    public void Every_label_names_its_field()
    {
        // 1.3.1, 4.1.2: a label beside a field names it only through "for".
        foreach (var file in Directory.GetFiles(DemosPath("Pages"), "*.razor", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(DemosPath("Components"), "*.razor")))
        {
            var text = File.ReadAllText(file);
            Assert.True(!Regex.IsMatch(text, @"<label>[^<]*</label>\s*<(input|select|textarea)\b"), $"{Path.GetFileName(file)} has a label without for");
            foreach (Match label in Regex.Matches(text, "<label for=\"(?<id>[^\"]+)\""))
                Assert.Contains($"id=\"{label.Groups["id"].Value}\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Focus_is_visible_and_clear_of_the_top_bar_and_pages_reflow_at_320_pixels()
    {
        var css = File.ReadAllText(DemosPath("wwwroot/css/app.css"));

        Assert.Contains("html { scroll-padding-top: calc(var(--app-topbar-h) + 0.5rem); }", css, StringComparison.Ordinal);
        Assert.Contains(".dropzone:focus-within { outline: 2px solid var(--app-focus); outline-offset: 2px; }", css, StringComparison.Ordinal);
        Assert.Matches(@"\.form-control:focus-within,[^{]*\{\s*outline: 2px solid var\(--app-focus\);", css);
        Assert.Contains(".field-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));", css, StringComparison.Ordinal);
        Assert.Contains(".field-row-3 { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr));", css, StringComparison.Ordinal);
        Assert.Contains("input[type=\"file\"] { max-width: 100%; }", css, StringComparison.Ordinal);
        Assert.Contains(".segmented button[aria-pressed=\"true\"] { background: var(--app-accent); color: #fff;", css, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--app-field-border); border-radius: 8px; background: #fff; }", css, StringComparison.Ordinal);

        // The phone overrides for controls styled later in the file come last, so they win.
        int last = css.LastIndexOf("@media (max-width: 575.98px)", StringComparison.Ordinal);
        Assert.True(last > css.IndexOf(".demo-choice-option {", StringComparison.Ordinal) && last > css.IndexOf(".gallery-tab {", StringComparison.Ordinal));
        Assert.Contains(".demo-choice .demo-choice-option { min-height: 44px; }", css[last..], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fontSize", "The value must be greater than zero. Arg_ParamName_Name, fontSize ArgumentOutOfRange_ActualValue, 0", "Font size: the value must be greater than zero.")]
    [InlineData("pagesPerPart", "Parts must have at least one page. (Parameter 'pagesPerPart')", "Pages per part: parts must have at least one page.")]
    [InlineData(null, "Choose at least one document.", "Choose at least one document.")]
    public void Errors_read_as_sentences_that_name_the_value(string? parameter, string message, string expected)
    {
        Exception exception = parameter is null ? new InvalidOperationException(message) : new ArgumentException(message, parameter);

        Assert.Equal(expected, ErrorText.Plain(exception));
        Assert.Contains("_error = ErrorText.Plain(exception);", File.ReadAllText(DemosPath("Components/SamplePage.razor.cs")), StringComparison.Ordinal);
        Assert.Contains("_error = ErrorText.Plain(exception);", File.ReadAllText(DemosPath("Components/SplitPartsPage.razor.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_in_another_language_says_which()
    {
        var input = new FontsAndLanguagesInput();
        var page = File.ReadAllText(DemosPath("Pages/Playground/Languages/FontsAndLanguages.razor"));

        input.Apply(LanguageInvoices.Find("hindi"));
        Assert.Equal("hi", input.LanguageTag);
        input.Apply(LanguageInvoices.Find("mixed"));
        Assert.Null(input.LanguageTag);
        Assert.Equal(5, Regex.Matches(page, "lang=\"@_input.LanguageTag\"").Count);
        Assert.Contains("ContentLanguage=\"@_input.LanguageTag\"", page, StringComparison.Ordinal);
        Assert.Contains("<div class=\"viewer-pages\" lang=\"@Language\">", File.ReadAllText(DemosPath("Components/PdfPreview.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_changes_are_announced_and_every_control_has_a_name()
    {
        var install = File.ReadAllText(DemosPath("Components/InstallCommand.razor"));
        var index = File.ReadAllText(DemosPath("wwwroot/index.html"));
        var code = File.ReadAllText(DemosPath("Components/CodeView.razor"));
        var css = File.ReadAllText(DemosPath("wwwroot/css/app.css"));

        Assert.Contains("role=\"status\">@(_copied ? \"Install command copied.\" : \"\")</span>", install, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Copy the install command\"", install, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\" class=\"dismiss\" aria-label=\"Dismiss\">", index, StringComparison.Ordinal);
        Assert.Contains("<pre tabindex=\"0\" role=\"region\" aria-label=\"@AriaLabel\">", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".faq-link-status:empty { display: none; }", css, StringComparison.Ordinal);
        Assert.Contains("_announcement = null; // the error box is an alert and announces itself", File.ReadAllText(DemosPath("Components/SamplePage.razor.cs")), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    private static string DemosPath(string relative, [CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative);

    #endregion

    #endregion
}
