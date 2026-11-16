using System.ComponentModel;
using System.Reflection;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The 1.0.0 public API (PR-016): one namespace, the renamed types and members, the removed 0.x names, visibility of
/// the renderer helpers, parameter names, and the <c>[AddedVersion]</c> each member records.
/// </summary>
public class PublicSurfaceTests
{
    #region Fields and Constants

    private const BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Assembly Library = typeof(PdfDocument).Assembly;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void AddText_on_a_page_has_a_double_overload_and_a_caller_font_overload_but_no_font_name()
    {
        var methods = typeof(PdfPage).GetMethods().Where(m => m.Name == nameof(PdfPage.AddText)).OrderBy(m => m.GetParameters().Length).ToList();

        Assert.Equal(2, methods.Count);
        Assert.Equal(new[] { typeof(string), typeof(double), typeof(double), typeof(double) }, methods[0].GetParameters().Select(p => p.ParameterType));
        Assert.Equal("text,x,y,fontSize", ParameterNames(methods[0]));
        Assert.Equal(new[] { typeof(string), typeof(PdfFont), typeof(double), typeof(double), typeof(double) }, methods[1].GetParameters().Select(p => p.ParameterType));
        Assert.Equal("text,font,x,y,fontSize", ParameterNames(methods[1]));
    }

    [Theory]
    [InlineData(typeof(PdfPage), nameof(PdfPage.AddText), "text,font,x,y,fontSize")]
    [InlineData(typeof(PdfSectionContent), nameof(PdfSectionContent.AddText), "text,font,x,y,fontSize,alignment")]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.AddText), "text,font,spacing,fontSize,alignment,x,keepWithNext")]
    [InlineData(typeof(PdfFlowBlock), nameof(PdfFlowBlock.AddText), "text,font,spacing,fontSize,alignment,x")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.SetText), "row,column,text,font")]
    public void Font_overloads_take_the_font_after_the_text(Type type, string name, string parameters)
    {
        // The font is part of the content, so the PR-016 order of position, size, spacing, and x after it is kept.
        var method = Assert.Single(type.GetMethods(PublicMembers), m => m.Name == name && TakesFont(m));

        Assert.Equal(parameters, ParameterNames(method));
        Assert.Equal("1.1.0", VersionOf(method));
        Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
    }

    [Theory]
    [InlineData(typeof(PdfPage), nameof(PdfPage.AddTable), "0.0.3")]
    [InlineData(typeof(PdfPageSizeExtensions), nameof(PdfPageSizeExtensions.ToDimensions), "0.0.2")]
    [InlineData(typeof(PdfRow), nameof(PdfRow.GetCell), "0.0.3")]
    [InlineData(typeof(PdfRow), nameof(PdfRow.SetCell), "0.0.3")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.AddColumn), "0.0.3")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.AddRow), "0.0.3")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.InsertColumn), "0.0.3")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.InsertRow), "0.0.3")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.SetText), "0.0.3")]
    public void Edited_shipped_methods_record_their_first_version_and_a_description(Type type, string name, string version)
    {
        var method = Assert.Single(type.GetMethods(PublicMembers), m => m.Name == name && !TakesFont(m));

        Assert.Equal(version, VersionOf(method));
        Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description));
    }

    [Fact]
    public void Every_public_type_is_in_the_UnlimitedPDF_namespace()
    {
        Assert.All(Library.GetExportedTypes(), type => Assert.Equal("UnlimitedPDF", type.Namespace));
    }

    [Theory]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.AddImage), "image,width,height,spacing,x,options,keepWithNext")]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.AddRectangle), "width,height,color,spacing,x,keepWithNext")]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.AddTable), "width,spacing,x,keepWithNext")]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.AddText), "text,spacing,fontSize,alignment,x,keepWithNext")]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.PageBreak), "")]
    [InlineData(typeof(PdfFlowBlock), nameof(PdfFlowBlock.AddImage), "image,width,height,spacing,x,options")]
    [InlineData(typeof(PdfFlowBlock), nameof(PdfFlowBlock.AddRectangle), "width,height,color,spacing,x")]
    [InlineData(typeof(PdfFlowBlock), nameof(PdfFlowBlock.AddTable), "width,spacing,x")]
    [InlineData(typeof(PdfFlowBlock), nameof(PdfFlowBlock.AddText), "text,spacing,fontSize,alignment,x")]
    [InlineData(typeof(PdfPage), nameof(PdfPage.AddImage), "image,x,y,width,height,options")]
    [InlineData(typeof(PdfPage), nameof(PdfPage.AddRectangle), "x,y,width,height,color")]
    [InlineData(typeof(PdfPage), nameof(PdfPage.AddTable), "x,y,width")]
    [InlineData(typeof(PdfSectionContent), nameof(PdfSectionContent.AddImage), "image,x,y,width,height,options")]
    [InlineData(typeof(PdfSectionContent), nameof(PdfSectionContent.AddRectangle), "x,y,width,height,color")]
    [InlineData(typeof(PdfSectionContent), nameof(PdfSectionContent.AddTable), "x,y,width")]
    [InlineData(typeof(PdfSectionContent), nameof(PdfSectionContent.AddText), "text,x,y,fontSize,alignment")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.EnsureSize), "rows,columns")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.GetCell), "row,column")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.GetOrCreateCell), "row,column")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.SetCell), "row,column,cell")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.SetText), "row,column,text")]
    [InlineData(typeof(PdfTable), nameof(PdfTable.TrimEmptyTrailingRowsAndColumns), "")]
    [InlineData(typeof(PdfRow), nameof(PdfRow.GetCell), "columnIndex")]
    [InlineData(typeof(PdfRow), nameof(PdfRow.SetCell), "columnIndex,cell")]
    [InlineData(typeof(PdfPageSection), nameof(PdfPageSection.SetPageContent), "build")]
    [InlineData(typeof(PdfColor), nameof(PdfColor.FromHex), "hex")]
    [InlineData(typeof(PdfColor), nameof(PdfColor.FromRgb), "r,g,b")]
    [InlineData(typeof(PdfPageSizeExtensions), nameof(PdfPageSizeExtensions.ToDimensions), "pageSize")]
    public void Methods_have_their_1_0_0_parameter_order(Type type, string name, string parameters)
    {
        // The 1.1.0 caller-font overloads are checked by Font_overloads_take_the_font_after_the_text.
        var method = Assert.Single(type.GetMethods(PublicMembers), m => m.Name == name && !TakesFont(m));
        Assert.Equal(parameters, ParameterNames(method));
    }

    [Fact]
    public void No_public_member_is_obsolete()
    {
        foreach (var type in Library.GetExportedTypes())
        {
            Assert.Null(type.GetCustomAttribute<ObsoleteAttribute>());
            Assert.All(type.GetMembers(PublicMembers | BindingFlags.DeclaredOnly), member => Assert.Null(member.GetCustomAttribute<ObsoleteAttribute>()));
        }
    }

    [Fact]
    public void PdfDocument_AddPageSettings_takes_a_target_or_a_page_number()
    {
        var overloads = typeof(PdfDocument).GetMethods(PublicMembers)
            .Where(m => m.Name == nameof(PdfDocument.AddPageSettings))
            .OrderBy(ParameterNames)
            .ToList();

        Assert.Equal(new[] { "pageNumber", "target" }, overloads.Select(ParameterNames));
        Assert.Equal(typeof(int), overloads[0].GetParameters().Single().ParameterType);
        Assert.Equal(typeof(PdfPageTarget), overloads[1].GetParameters().Single().ParameterType);
        Assert.All(overloads, method => Assert.Equal(typeof(PdfPageSettings), method.ReturnType));
        Assert.All(overloads, method => Assert.Equal("1.0.0", VersionOf(method)));
        Assert.Contains(typeof(PdfPageSettings), Library.GetExportedTypes());
        Assert.Contains(typeof(PdfPageTarget), Library.GetExportedTypes());
        Assert.Equal(new[] { "First", "Last", "Odd", "Even" }, Enum.GetNames(typeof(PdfPageTarget)));
    }

    [Fact]
    public void PdfDocument_AddPage_returns_PdfPage()
    {
        Assert.Equal(typeof(PdfPage), typeof(PdfDocument).GetMethod(nameof(PdfDocument.AddPage))!.ReturnType);
    }

    [Fact]
    public void PdfRow_constructor_spells_out_its_column_count()
    {
        var constructor = Assert.Single(typeof(PdfRow).GetConstructors());
        Assert.Equal("index,initialColumns", ParameterNames(constructor));
    }

    [Theory]
    [InlineData(typeof(PdfCell), nameof(PdfCell.BackgroundColor), typeof(PdfColor))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.BorderBottom), typeof(PdfBorder))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.BorderLeft), typeof(PdfBorder))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.BorderRight), typeof(PdfBorder))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.BorderTop), typeof(PdfBorder))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.ColumnSpan), typeof(int))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.HorizontalAlignment), typeof(PdfCellHorizontalAlignment))]
    [InlineData(typeof(PdfCell), nameof(PdfCell.VerticalAlignment), typeof(PdfCellVerticalAlignment))]
    [InlineData(typeof(PdfTable), nameof(PdfTable.ColumnCount), typeof(int))]
    [InlineData(typeof(PdfTable), nameof(PdfTable.RowHeightMode), typeof(PdfRowHeightMode))]
    [InlineData(typeof(PdfRow), nameof(PdfRow.HeightMode), typeof(PdfRowHeightMode?))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.BodyMarginBottom), typeof(double))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.BodyMarginTop), typeof(double))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.MarginBottom), typeof(double))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.MarginLeft), typeof(double))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.MarginRight), typeof(double))]
    [InlineData(typeof(PdfDocument), nameof(PdfDocument.MarginTop), typeof(double))]
    [InlineData(typeof(PdfPageSettings), nameof(PdfPageSettings.BodyMarginBottom), typeof(double?))]
    [InlineData(typeof(PdfPageSettings), nameof(PdfPageSettings.BodyMarginTop), typeof(double?))]
    [InlineData(typeof(PdfPageSettings), nameof(PdfPageSettings.MarginLeft), typeof(double?))]
    [InlineData(typeof(PdfPageSettings), nameof(PdfPageSettings.MarginRight), typeof(double?))]
    [InlineData(typeof(PdfBodyFlow), nameof(PdfBodyFlow.RemainingHeight), typeof(double))]
    public void Renamed_properties_exist_and_first_ship_in_1_0_0(Type type, string name, Type propertyType)
    {
        var property = type.GetProperty(name, PublicMembers);

        Assert.NotNull(property);
        Assert.Equal(propertyType, property!.PropertyType);
        Assert.Equal("1.0.0", VersionOf(property));
    }

    [Theory]
    [InlineData(typeof(PdfCell), "Background")]
    [InlineData(typeof(PdfCell), "BottomBorder")]
    [InlineData(typeof(PdfCell), "ColSpan")]
    [InlineData(typeof(PdfCell), "HAlign")]
    [InlineData(typeof(PdfCell), "LeftBorder")]
    [InlineData(typeof(PdfCell), "RightBorder")]
    [InlineData(typeof(PdfCell), "TopBorder")]
    [InlineData(typeof(PdfCell), "VAlign")]
    [InlineData(typeof(PdfTable), "ColCount")]
    [InlineData(typeof(PdfTable), "GetEffectiveColumnWidths")]
    [InlineData(typeof(PdfTable), "IsSameSpanCell")]
    [InlineData(typeof(PdfTable), "NormalizeSpans")]
    [InlineData(typeof(PdfTable), "RowHeightRule")]
    [InlineData(typeof(PdfTable), "TrimEmptyTrailingRowsAndCols")]
    [InlineData(typeof(PdfRow), "HeightRule")]
    [InlineData(typeof(PdfDocument), "AddPageRule")]
    [InlineData(typeof(PdfDocument), "BottomMargin")]
    [InlineData(typeof(PdfDocument), "LeftMargin")]
    [InlineData(typeof(PdfDocument), "RightMargin")]
    [InlineData(typeof(PdfDocument), "TopMargin")]
    [InlineData(typeof(PdfPageSettings), "BodyBottomGap")]
    [InlineData(typeof(PdfPageSettings), "BodyTopGap")]
    [InlineData(typeof(PdfPageSettings), "LeftMargin")]
    [InlineData(typeof(PdfPageSettings), "RightMargin")]
    [InlineData(typeof(PdfBodyFlow), "BottomGap")]
    [InlineData(typeof(PdfBodyFlow), "Remaining")]
    [InlineData(typeof(PdfBodyFlow), "TopGap")]
    [InlineData(typeof(PdfPageSection), "Build")]
    [InlineData(typeof(PdfPageSizeExtensions), "ToMediaBoxString")]
    public void Removed_and_internal_members_are_not_public(Type type, string name)
    {
        Assert.Empty(type.GetMember(name, PublicMembers));
    }

    [Theory]
    [InlineData("AddedVersionAttribute")]
    [InlineData("PdfPageBuilder")]
    [InlineData("PdfPageRule")]
    [InlineData("PdfPageRuleTarget")]
    [InlineData("PdfRowHeightRule")]
    [InlineData("PdfTableCell")]
    [InlineData("PdfTableColumn")]
    [InlineData("PdfTableRow")]
    public void Removed_and_internal_types_are_not_public(string name)
    {
        Assert.DoesNotContain(Library.GetExportedTypes(), type => type.Name == name);
    }

    [Fact]
    public void Split_api_has_the_approved_names_and_first_ships_in_1_0_0()
    {
        var split = typeof(PdfOperations).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name is nameof(PdfOperations.Split) or nameof(PdfOperations.SplitAsync))
            .ToList();
        Assert.Equal(new[]
        {
            "Split(file,pagesPerPart,openPart)", "Split(file,pagesPerPart,outputFolder)",
            "Split(file,ranges,openPart)", "Split(file,ranges,outputFolder)",
            "SplitAsync(file,pagesPerPart,openPart,cancellationToken)", "SplitAsync(file,pagesPerPart,outputFolder,cancellationToken)",
            "SplitAsync(file,ranges,openPart,cancellationToken)", "SplitAsync(file,ranges,outputFolder,cancellationToken)",
        }, split.Select(m => $"{m.Name}({ParameterNames(m)})").OrderBy(s => s, StringComparer.Ordinal));
        Assert.All(split.Where(m => m.Name == nameof(PdfOperations.Split)), m => Assert.Equal(typeof(IReadOnlyList<PdfSplitPart>), m.ReturnType));
        Assert.All(split.Where(m => m.Name == nameof(PdfOperations.SplitAsync)), m => Assert.Equal(typeof(Task<IReadOnlyList<PdfSplitPart>>), m.ReturnType));
        Assert.All(split, m => Assert.Contains(m.GetParameters()[2].ParameterType, new[] { typeof(string), typeof(Func<PdfSplitPart, Stream>) }));

        var range = Assert.Single(typeof(PdfPageRange).GetConstructors());
        Assert.Equal("firstPage,lastPage", ParameterNames(range));
        Assert.Empty(typeof(PdfSplitPart).GetConstructors());
        Assert.Equal(new[] { "FileName", "FirstPage", "LastPage", "Number" }, typeof(PdfSplitPart).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var exception = Assert.Single(typeof(PdfSplitException).GetConstructors());
        Assert.Equal("message,partNumber,partsWritten,innerException", ParameterNames(exception));
        Assert.NotNull(typeof(PdfSplitException).GetProperty(nameof(PdfSplitException.PartNumber)));
        Assert.NotNull(typeof(PdfSplitException).GetProperty(nameof(PdfSplitException.PartsWritten)));

        var members = split.Cast<MethodBase>().Append(range).Append(exception).Append(typeof(PdfPageRange).GetMethod(nameof(ToString))!);
        foreach (var member in members)
        {
            Assert.Equal("1.0.0", VersionOf(member));
            Assert.False(string.IsNullOrWhiteSpace(member.GetCustomAttribute<DescriptionAttribute>()?.Description));
        }
    }

    [Fact]
    public void Table_building_helpers_and_position_stay_public()
    {
        Assert.NotNull(typeof(PdfTable).GetMethod(nameof(PdfTable.EnsureSize)));
        Assert.NotNull(typeof(PdfTable).GetMethod(nameof(PdfTable.TrimEmptyTrailingRowsAndColumns)));
        Assert.NotNull(typeof(PdfTable).GetProperty(nameof(PdfTable.X))?.SetMethod);
        Assert.NotNull(typeof(PdfTable).GetProperty(nameof(PdfTable.Y))?.SetMethod);
    }

    [Fact]
    public void Versions_record_when_each_name_first_shipped()
    {
        foreach (var type in Library.GetExportedTypes())
            Assert.All(type.GetMembers(PublicMembers), member => Assert.NotEqual("0.1.0", VersionOf(member)));

        Assert.Equal("1.0.0", VersionOf(typeof(PdfTable).GetMethod(nameof(PdfTable.TrimEmptyTrailingRowsAndColumns))!));
        Assert.Equal("1.0.0", VersionOf(typeof(PdfPageSection).GetMethod(nameof(PdfPageSection.SetPageContent))!));
        Assert.Equal("1.0.0", VersionOf(typeof(PdfColor).GetMethod(nameof(PdfColor.FromHex))!));
        Assert.Equal("0.0.3", VersionOf(typeof(PdfTable).GetMethod(nameof(PdfTable.GetCell))!));
        Assert.Equal("0.0.2", VersionOf(typeof(PdfPage).GetMethod(nameof(PdfPage.AddText), new[] { typeof(string), typeof(double), typeof(double), typeof(double) })!));
        Assert.Equal("1.1.0", VersionOf(typeof(PdfFont).GetMethod(nameof(PdfFont.FromFile))!));
    }

    [Fact]
    public void Version_attribute_is_internal_and_readable_by_name()
    {
        Assert.False(typeof(AddedVersionAttribute).IsPublic);

        var data = typeof(PdfTable).GetMethod(nameof(PdfTable.GetCell))!.GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType.Name == "AddedVersionAttribute");
        Assert.Equal("0.0.3", data.ConstructorArguments.Single().Value);
    }

    #endregion

    #region Private Methods

    // True for the 1.1.0 overloads that take a caller font (WR-039).
    private static bool TakesFont(MethodBase method) => method.GetParameters().Any(p => p.ParameterType == typeof(PdfFont));

    private static string ParameterNames(MethodBase method) => string.Join(",", method.GetParameters().Select(p => p.Name));

    private static string? VersionOf(MemberInfo member) => member.GetCustomAttribute<AddedVersionAttribute>()?.Version;

    #endregion

    #endregion
}
