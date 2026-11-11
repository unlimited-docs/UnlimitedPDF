namespace UnlimitedPDF.Models.Internal.Html.Css;

/// <summary>
/// The built-in default stylesheet (WR-038, AC-05): the HTML Standard's suggested default rendering for the supported
/// elements, so HTML without author styles looks as it does in a browser. Text defaults to the serif family, as in
/// browsers; 1em is 12 pt (16 px).
/// </summary>
internal static class UserAgentStyles
{
    #region Fields and Constants

    public const string Css = @"
html, body, div, p, blockquote, pre, address, section, article, header, footer, main, nav, aside, figure, figcaption,
h1, h2, h3, h4, h5, h6, ul, ol, dl, dt, dd, hr, center, details, summary, fieldset, form, legend, menu, dir { display: block }
head, title, style, script, meta, link, base { display: none }
li { display: list-item }
table { display: table; border-collapse: separate; border-spacing: 2px; border-color: gray }
caption { display: table-caption; text-align: center }
thead { display: table-header-group; vertical-align: middle }
tbody { display: table-row-group; vertical-align: middle }
tfoot { display: table-footer-group; vertical-align: middle }
tr { display: table-row; vertical-align: inherit }
td, th { display: table-cell; vertical-align: inherit; padding: 1px }
th { font-weight: bold; text-align: center }
col { display: table-column }
colgroup { display: table-column-group }
body { margin: 8px }
p, dl, figure { margin-top: 1em; margin-bottom: 1em }
blockquote, figure { margin: 1em 40px }
ul, ol { margin-top: 1em; margin-bottom: 1em; padding-left: 40px }
ul ul, ul ol, ol ul, ol ol, ul dl, ol dl, dl ul, dl ol, dl dl { margin-top: 0; margin-bottom: 0 }
ul { list-style-type: disc }
ol { list-style-type: decimal }
ul ul, ol ul { list-style-type: circle }
ul ul ul, ul ol ul, ol ul ul, ol ol ul { list-style-type: square }
dd { margin-left: 40px }
h1 { font-size: 2em; margin-top: 0.67em; margin-bottom: 0.67em; font-weight: bold }
h2 { font-size: 1.5em; margin-top: 0.83em; margin-bottom: 0.83em; font-weight: bold }
h3 { font-size: 1.17em; margin-top: 1em; margin-bottom: 1em; font-weight: bold }
h4 { font-size: 1em; margin-top: 1.33em; margin-bottom: 1.33em; font-weight: bold }
h5 { font-size: 0.83em; margin-top: 1.67em; margin-bottom: 1.67em; font-weight: bold }
h6 { font-size: 0.67em; margin-top: 2.33em; margin-bottom: 2.33em; font-weight: bold }
pre { white-space: pre; margin-top: 1em; margin-bottom: 1em }
pre, code, kbd, samp, tt { font-family: monospace }
b, strong { font-weight: bold }
i, em, address, cite, var, dfn { font-style: italic }
u, ins { text-decoration: underline }
s, del, strike { text-decoration: line-through }
small { font-size: smaller }
sub { vertical-align: sub; font-size: smaller }
sup { vertical-align: super; font-size: smaller }
mark { background-color: yellow; color: black }
a[href] { color: #0000EE; text-decoration: underline }
hr { border-top: 1px solid gray; border-bottom: 1px solid gray; margin-top: 0.5em; margin-bottom: 0.5em }
center { text-align: center }
";

    #endregion
}
