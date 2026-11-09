using System.ComponentModel;
using System.Reflection;

namespace UnlimitedPDF.Tests.Html;

/// <summary>
/// The HTML load surface matches the approved API (D-019, PR-016, PR-017, CS-013): names, parameters, and metadata.
/// </summary>
public class HtmlPublicSurfaceTests
{
    #region Methods

    #region Public Methods

    [Theory]
    [InlineData(nameof(PdfHtml.FromFile), "path,options")]
    [InlineData(nameof(PdfHtml.FromFileAsync), "path,options,cancellationToken")]
    [InlineData(nameof(PdfHtml.FromStream), "stream,options")]
    [InlineData(nameof(PdfHtml.FromStreamAsync), "stream,options,cancellationToken")]
    [InlineData(nameof(PdfHtml.FromString), "html,options")]
    [InlineData(nameof(PdfHtml.FromStringAsync), "html,options,cancellationToken")]
    public void Load_methods_have_the_approved_parameters_and_metadata(string name, string parameters)
    {
        var method = Assert.Single(typeof(PdfHtml).GetMethods(BindingFlags.Public | BindingFlags.Static), m => m.Name == name);
        Assert.Equal(parameters, string.Join(",", method.GetParameters().Select(p => p.Name)));
        Assert.Equal("1.0.0", method.GetCustomAttribute<AddedVersionAttribute>()!.Version);
        Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()!.Description));
    }

    [Fact]
    public void Load_types_have_the_approved_members()
    {
        Assert.Equal(new[] { "DefaultMaxInputSize", "DefaultMaxNestingDepth", "DefaultMaxResourceSize", "DefaultMaxTotalResourceSize", "Warnings" },
            typeof(PdfHtml).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(typeof(PdfHtml).GetConstructors());
        Assert.Equal(new[] { "BaseFolder", "FailOnWarning", "MaxInputSize", "MaxNestingDepth", "MaxResourceSize", "MaxTotalResourceSize", "ResourceResolver", "ResourceResolverAsync" },
            typeof(PdfHtmlLoadOptions).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "Kind", "Line", "Message", "PageNumber", "Source" },
            typeof(PdfHtmlWarning).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "Reason", "Warning" },
            typeof(PdfHtmlException).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "UnsupportedElement", "UnsupportedProperty", "UnsupportedValue", "ResourceNotFound", "ResourceBlocked", "ResourceNotResolved", "UnsupportedImageFormat", "InvalidImage", "LinkDropped", "ContentClipped" },
            Enum.GetNames(typeof(PdfHtmlWarningKind)));
        Assert.Equal(new[] { "InputTooLarge", "NestingTooDeep", "ResourceTooLarge", "ResourcesTooLarge", "Warning" },
            Enum.GetNames(typeof(PdfHtmlErrorReason)));
    }

    [Fact]
    public void Html_types_are_in_the_UnlimitedPDF_namespace()
    {
        foreach (var type in new[] { typeof(PdfHtml), typeof(PdfHtmlLoadOptions), typeof(PdfHtmlWarning), typeof(PdfHtmlException), typeof(PdfHtmlWarningKind), typeof(PdfHtmlErrorReason) })
            Assert.Equal("UnlimitedPDF", type.Namespace);
    }

    #endregion

    #endregion
}
