using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Tests;

/// <summary>
/// Usability guarantees of the demos Home page, Playground, and FAQ that markup and styles can prove: the skip link,
/// the keyboard focus ring, phone tap targets, the current demo kept in view in the sidebar, and "Back to options"
/// (26111103 V-03); the FAQ search field border, the copy-link icon, the FAQ phone sizes, the scenario list's inset
/// ring, and the gap above Remove buttons (26111201 V-03). Contrast, sizes, and fit are measured in the browser
/// (26111103 V-01, V-02; 26111201 V-01, V-02).
/// </summary>
public class DemoUsabilityTests
{
    #region Fields and Constants

    private const string Site = "UnlimitedPDF.Demos.BlazorWasm/";

    #endregion

    #region Methods

    #region Public Methods

    [Theory]
    [InlineData("Components/SamplePage")]
    [InlineData("Components/SplitPartsPage")]
    public void A_narrow_output_offers_back_to_options(string page)
    {
        var markup = Read(page + ".razor");
        var code = Read(page + ".razor.cs");

        Assert.Contains("id=\"@($\"{_id}-options\")\" tabindex=\"-1\" @ref=\"_optionsSection\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn-sm btn-text back-to-options\" @onclick=\"BackToOptionsAsync\"", markup, StringComparison.Ordinal);
        Assert.Contains("> Back to options", markup, StringComparison.Ordinal);
        Assert.Contains("await Interop.ScrollToElementAsync($\"{_id}-options\", smooth: true)", code, StringComparison.Ordinal);

        // Shown only where the options sit above the output.
        var css = Read("wwwroot/css/app.css");
        Assert.Contains(".output-actions .btn.back-to-options { display: none; }", css, StringComparison.Ordinal);
        Assert.Matches(@"@media \(max-width: 1279\.98px\) \{\s*\.output-actions \.btn\.back-to-options \{ display: inline-flex;", css);
    }

    [Fact]
    public void Changed_components_never_render_raw_markup()
    {
        // A guard against typed option values reaching the page as markup (A05).
        var files = new[]
        {
            "Layout/MainLayout.razor", "Layout/MainLayout.razor.cs", "Layout/PlaygroundLayout.razor", "Layout/PlaygroundLayout.razor.cs",
            "Components/SamplePage.razor", "Components/SamplePage.razor.cs", "Components/SplitPartsPage.razor", "Components/SplitPartsPage.razor.cs",
            "Pages/Playground/Create/HeadersFooters.razor", "Pages/Playground/Documents/EmployeeDirectory.razor",
            "Components/FaqQuestionItem.razor", "Components/Icon.razor", "Pages/Playground/Html/CommonProblems.razor",
            "Pages/Playground/Merge/BookmarksAndNumbers.razor", "Pages/Playground/Merge/CoverAndRanges.razor"
        };

        Assert.All(files, file => Assert.DoesNotContain("MarkupString", Read(file), StringComparison.Ordinal));

        var script = Read("wwwroot/js/app.js");
        Assert.All(new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "eval(" }, sink => Assert.DoesNotContain(sink, script, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_page_starts_with_a_skip_link_to_its_main_content()
    {
        var layout = Read("Layout/MainLayout.razor");

        int skip = layout.IndexOf("<a class=\"skip-link\" href=\"#main-content\"", StringComparison.Ordinal);
        int firstOther = Regex.Matches(layout, "<(a|NavLink|button)\\b").Select(m => m.Index).Where(i => i != skip).Min();
        Assert.True(skip >= 0 && skip < firstOther, "The skip link is the first focusable element of the layout.");
        Assert.Contains(">Skip to content</a>", layout, StringComparison.Ordinal);
        Assert.Contains("@onclick:preventDefault", layout, StringComparison.Ordinal);
        Assert.Matches("<main id=\"main-content\"[^>]*tabindex=\"-1\"", layout);
        Assert.Contains("FocusSkipTargetAsync", Read("Layout/MainLayout.razor.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_page_ends_with_the_site_footer()
    {
        // QF-26111207: the footer is in the layout, after the page content, so every page shows it, and Home has no
        // second one.
        var layout = Read("Layout/MainLayout.razor");

        int main = layout.IndexOf("</main>", StringComparison.Ordinal);
        int footer = layout.IndexOf("<footer class=\"footer\">", StringComparison.Ordinal);
        Assert.True(main >= 0 && footer > main, "The footer follows the page content.");
        Assert.Contains("Designed and built with <Icon Name=\"heart\"", layout, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">love</span>", layout, StringComparison.Ordinal);
        Assert.Contains(">UnlimitedDocs team</a>", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/unlimited-docs/UnlimitedPDF/graphs/contributors\"", layout, StringComparison.Ordinal);
        Assert.Contains("Powered by .NET 11 · Open source under the <span class=\"text-nowrap\">Apache-2.0</span> license · Demo data is fictional.", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<footer", Read("Pages/Home.razor"), StringComparison.Ordinal);
    }

    [Fact]
    public void Faq_topic_chips_and_clear_search_are_44_pixels_on_phones()
    {
        var phone = string.Join("\n", PhoneBlocks(Read("wwwroot/css/app.css")));

        // Topic chips have their own rule; "Clear search" is a .btn and takes the shared phone size (26111201 AC-03, AC-04).
        Assert.Matches(new Regex(@"(^|[,}])\s*\.faq-index a\s*\{[^}]*min-height: 44px", RegexOptions.Multiline), phone);
        Assert.Matches(new Regex(@"(^|[,}])\s*\.btn\s*[,{][^}]*min-height: 44px", RegexOptions.Multiline), phone);
        Assert.Matches("<button type=\"button\" class=\"btn [^\"]*\" @onclick=\"ClearSearchAsync\">Clear search</button>", Read("Pages/Faq.razor"));
    }

    [Fact]
    public void Links_buttons_tabs_and_fields_share_one_solid_focus_ring()
    {
        var css = Read("wwwroot/css/app.css");
        var rule = Regex.Match(css, @"((?:[^{}]*:focus-visible,\s*)+[^{}]*:focus-visible)\s*\{\s*outline: 2px solid var\(--app-accent\);\s*outline-offset: 2px;\s*\}");

        Assert.True(rule.Success, "A shared :focus-visible rule draws a 2 px accent outline.");
        var selectors = rule.Groups[1].Value;
        Assert.All(
            new[] { "a:focus-visible", "button:focus-visible", ".btn:focus-visible", "[role=\"tab\"]:focus-visible", "input:focus-visible", "select:focus-visible", "textarea:focus-visible" },
            selector => Assert.Contains(selector, selectors, StringComparison.Ordinal));
    }

    [Fact]
    public void Phone_controls_are_at_least_44_pixels()
    {
        var css = Read("wwwroot/css/app.css");
        var phone = string.Join("\n", PhoneBlocks(css));

        Assert.All(
            new[]
            {
                ".brand", ".topnav-link", ".btn", ".output-tab", ".sidebar-link", ".form-control", ".row-remove", ".check-field",
                ".install button", ".jump-link", ".questions-more", ".tile-actions .btn", ".ql-icon-button", ".ql-nav .btn"
            },
            // The selector starts a rule (line start, after a comma, or after a closing brace), so ".btn" does not match
            // inside ".tile-actions .btn".
            selector => Assert.Matches(new Regex(@"(^|[,}])\s*" + Regex.Escape(selector) + @"\s*[,{][^}]*min-height: 44px", RegexOptions.Multiline), phone));
    }

    [Theory]
    [InlineData("Pages/Playground/Merge/BookmarksAndNumbers.razor")]
    [InlineData("Pages/Playground/Merge/CoverAndRanges.razor")]
    public void Remove_buttons_keep_a_gap_below_their_field(string page)
    {
        // The same gap as between fields, instead of touching the field above (26111201 AC-06).
        // The tag's own @onclick holds "=>", so the match runs to the button's text rather than to the first ">".
        var remove = Regex.Match(Read(page), "<button type=\"button\" class=\"(?<classes>[^\"]*)\"(?:(?!</button>).)*?>Remove</button>", RegexOptions.Singleline);

        Assert.True(remove.Success, "The page has a Remove button.");
        Assert.Contains("field-action", remove.Groups["classes"].Value.Split(' '));
        Assert.Contains("margin-top: 0.7rem;", RuleBody(Read("wwwroot/css/app.css"), ".field-action"), StringComparison.Ordinal);
    }

    [Fact]
    public void Scrolling_to_the_output_honors_reduced_motion()
    {
        var script = Read("wwwroot/js/app.js");
        var helper = Regex.Match(script, @"export function scrollIntoViewOnNarrow\(element\) \{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;

        Assert.Contains("(prefers-reduced-motion: reduce)", helper, StringComparison.Ordinal);
        Assert.Contains("behavior: reduced ? \"auto\" : \"smooth\"", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_copy_button_gets_a_phone_tap_area_without_growing_over_the_code()
    {
        var css = Read("wwwroot/css/app.css");
        var phone = string.Join("\n", PhoneBlocks(css));

        // A transparent layer around the button is the target; the button itself keeps its size (26111103 R-02).
        Assert.Contains(".code-copy::before { content: \"\"; position: absolute; inset: -7px -4px; }", phone, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"(^|[,}])\s*\.code-copy\s*\{[^}]*min-height", RegexOptions.Multiline), phone);
    }

    [Fact]
    public void The_current_demo_is_brought_into_view_in_the_sidebar()
    {
        var layout = Read("Layout/PlaygroundLayout.razor.cs");
        var script = Read("wwwroot/js/app.js");

        Assert.Contains("Navigation.LocationChanged += OnLocationChanged", layout, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged -= OnLocationChanged", layout, StringComparison.Ordinal);
        Assert.Contains("await Interop.RevealActiveLinkAsync(_sidebar)", layout, StringComparison.Ordinal);

        // The helper does nothing when the sidebar is missing or hidden (a closed phone menu), and scrolls only the
        // sidebar, never the page.
        var helper = Regex.Match(script, @"export function revealActiveLink\(container\) \{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;
        Assert.Contains("if (!container || container.offsetParent === null)", helper, StringComparison.Ordinal);
        Assert.Contains("container.scrollTop", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("scrollIntoView", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("window.scroll", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void The_faq_copy_link_control_shows_a_link_icon_in_the_muted_color()
    {
        var item = Read("Components/FaqQuestionItem.razor");
        var button = Regex.Match(item, "<button type=\"button\" class=\"faq-link\"[^>]*>(?<content>.*?)</button>", RegexOptions.Singleline);

        // The icon replaces the "#" glyph; the spoken label and the copy behavior stay (26111201 AC-02).
        Assert.True(button.Success, "The copy-link button is present.");
        Assert.Equal("<Icon Name=\"link\" Size=\"18\" />", button.Groups["content"].Value.Trim());
        Assert.Contains("aria-label=\"@($\"Copy a link to this question: {Question.Text}\")\"", button.Value, StringComparison.Ordinal);
        Assert.Contains("@onclick=\"CopyLinkAsync\"", button.Value, StringComparison.Ordinal);
        Assert.Contains("case \"link\":", Read("Components/Icon.razor"), StringComparison.Ordinal);
        Assert.Contains("color: var(--app-muted);", RuleBody(Read("wwwroot/css/app.css"), ".faq-link"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_faq_search_field_uses_the_shared_field_border()
    {
        // 3.5:1 on white instead of the earlier 1.35:1 (26111201 AC-01).
        var rule = RuleBody(Read("wwwroot/css/app.css"), ".faq-search-input");

        Assert.Contains("border: 1px solid var(--app-field-border);", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void The_playground_skip_target_is_the_demo_past_the_sidebar()
    {
        var playground = Read("Layout/PlaygroundLayout.razor");

        Assert.Matches("<div class=\"playground-content\" data-skip-target tabindex=\"-1\">", playground);
        Assert.True(
            playground.IndexOf("playground-sidebar", StringComparison.Ordinal) < playground.IndexOf("data-skip-target", StringComparison.Ordinal),
            "The skip target comes after the sidebar.");

        var script = Read("wwwroot/js/app.js");
        Assert.Contains("export function focusSkipTarget()", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelector(\"[data-skip-target]\") ?? document.getElementById(\"main-content\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scenario_list_draws_its_focus_ring_inside_each_link()
    {
        // The list scrolls on its own below 1,280 px, so an outer ring would be clipped at its edges (26111201 AC-05).
        Assert.Contains(".scenario-list a:focus-visible { outline-offset: -2px; }", Read("wwwroot/css/app.css"), StringComparison.Ordinal);
    }

    #endregion

    #region Private Methods

    // The bodies of the stylesheet's phone media blocks (max-width 575.98px).
    private static IEnumerable<string> PhoneBlocks(string css)
    {
        const string Start = "@media (max-width: 575.98px) {";
        for (int at = css.IndexOf(Start, StringComparison.Ordinal); at >= 0; at = css.IndexOf(Start, at + 1, StringComparison.Ordinal))
        {
            int depth = 1, i = at + Start.Length;
            for (; i < css.Length && depth > 0; i++)
            {
                depth += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
            }

            yield return css[(at + Start.Length)..(i - 1)];
        }
    }

    private static string Read(string relative) => File.ReadAllText(RepositoryFile(Site + relative));

    private static string RepositoryFile(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", relative));

    // The declarations of the first rule, in any block, whose selector is exactly the given one.
    private static string RuleBody(string css, string selector)
    {
        var rule = Regex.Match(css, @"(^|\})\s*" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}", RegexOptions.Multiline);
        Assert.True(rule.Success, $"The stylesheet has a {selector} rule.");
        return rule.Groups["body"].Value;
    }

    #endregion

    #endregion
}
