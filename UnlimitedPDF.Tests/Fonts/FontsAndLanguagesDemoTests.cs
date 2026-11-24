using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Demos.BlazorWasm.Services;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// The Fonts and languages demo (CR-002, AC-17 to AC-24): its font slots, invoice presets, editable input, and font
/// loading, with fonts generated in test code (PR-011, D-014).
/// </summary>
public class FontsAndLanguagesDemoTests
{
    #region Fields and Constants

    private const string Devanagari = "कखगचजटडतदनपबमयरलवसह";
    private const string Telugu = "కఖగచజటడతదనపబమయరలవసహ";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void The_presets_are_the_nine_indian_languages_eight_european_ones_filipino_and_mixed_with_their_money_style()
    {
        Assert.Equal(
            new[] { "hindi", "telugu", "tamil", "bengali", "gujarati", "punjabi", "odia", "kannada", "malayalam", "french", "polish", "czech", "turkish", "filipino", "greek", "russian", "ukrainian", "armenian", "georgian", "mixed" },
            LanguageInvoices.All.Select(p => p.Id));
        Assert.All(LanguageInvoices.All, p => Assert.True(p.Items.Count >= 2, p.Id));
        Assert.Equal("₹ 1,500.00", LanguageInvoices.Find("telugu").Money(1500m));
        Assert.Equal("1 500,50 €", LanguageInvoices.Find("french").Money(1500.5m));
        Assert.Equal("1 500,50 zł", LanguageInvoices.Find("polish").Money(1500.5m));
        Assert.Equal("396,00 Kč", LanguageInvoices.Find("czech").Money(396m));
        Assert.Equal("₱1,500.50", LanguageInvoices.Find("filipino").Money(1500.5m));
        Assert.Equal("15 000,00 ֏", LanguageInvoices.Find("armenian").Money(15000m));
        Assert.Same(LanguageInvoices.All[0], LanguageInvoices.Find("klingon"));
    }

    [Theory]
    [InlineData("bengali", "bn")]
    [InlineData("gujarati", "gu")]
    [InlineData("punjabi", "pa")]
    [InlineData("odia", "or")]
    [InlineData("kannada", "kn")]
    [InlineData("malayalam", "ml")]
    [InlineData("polish", "pl")]
    [InlineData("czech", "cs")]
    [InlineData("turkish", "tr")]
    [InlineData("filipino", "fil")]
    [InlineData("greek", "el")]
    [InlineData("russian", "ru")]
    [InlineData("ukrainian", "uk")]
    [InlineData("armenian", "hy")]
    [InlineData("georgian", "ka")]
    public void Each_added_language_invoice_names_its_language_for_the_page(string id, string tag)
    {
        var input = new FontsAndLanguagesInput();
        input.Apply(LanguageInvoices.Find(id));

        Assert.Equal(id, input.Language);
        Assert.Equal(tag, input.LanguageTag);
    }

    [Fact]
    public void Each_invoice_needs_the_slots_of_the_scripts_it_uses_in_slot_order()
    {
        IReadOnlyList<FontScript> Needs(string id)
        {
            var input = new FontsAndLanguagesInput();
            input.Apply(LanguageInvoices.Find(id));
            return FontSlots.Needed(input.AllText());
        }

        Assert.Equal(new[] { FontScript.Devanagari }, Needs("hindi"));
        Assert.Equal(new[] { FontScript.Telugu }, Needs("telugu"));
        Assert.Equal(new[] { FontScript.Tamil }, Needs("tamil"));
        Assert.Equal(new[] { FontScript.Bengali }, Needs("bengali"));
        Assert.Equal(new[] { FontScript.Gujarati }, Needs("gujarati"));
        Assert.Equal(new[] { FontScript.Gurmukhi }, Needs("punjabi"));
        Assert.Equal(new[] { FontScript.Odia }, Needs("odia"));
        Assert.Equal(new[] { FontScript.Kannada }, Needs("kannada"));
        Assert.Equal(new[] { FontScript.Malayalam }, Needs("malayalam"));
        Assert.Equal(new[] { FontScript.Latin }, Needs("french"));
        Assert.Equal(new[] { FontScript.Latin }, Needs("polish"));
        Assert.Equal(new[] { FontScript.Latin }, Needs("czech"));
        Assert.Equal(new[] { FontScript.Latin }, Needs("turkish"));
        Assert.Equal(new[] { FontScript.Latin }, Needs("filipino"));
        Assert.Equal(new[] { FontScript.Greek }, Needs("greek"));
        Assert.Equal(new[] { FontScript.Cyrillic }, Needs("russian"));
        Assert.Equal(new[] { FontScript.Cyrillic }, Needs("ukrainian"));
        Assert.Equal(new[] { FontScript.Armenian }, Needs("armenian"));
        Assert.Equal(new[] { FontScript.Georgian }, Needs("georgian"));
        Assert.Equal(new[] { FontScript.Latin, FontScript.Devanagari, FontScript.Telugu }, Needs("mixed"));
        Assert.Equal(new[]
        {
            FontScript.Latin, FontScript.Greek, FontScript.Cyrillic, FontScript.Armenian, FontScript.Georgian, FontScript.Devanagari,
            FontScript.Telugu, FontScript.Tamil, FontScript.Bengali, FontScript.Gujarati,
            FontScript.Gurmukhi, FontScript.Odia, FontScript.Kannada, FontScript.Malayalam
        }, FontSlots.Scripts);
        Assert.Null(FontSlots.ScriptOf('×'));
        Assert.Null(FontSlots.ScriptOf('₹'));
        Assert.Null(FontSlots.ScriptOf('₽'));
        Assert.Equal(FontScript.Greek, FontSlots.ScriptOf('ά'));
        Assert.Equal(FontScript.Cyrillic, FontSlots.ScriptOf('ї'));
        Assert.Equal(FontScript.Armenian, FontSlots.ScriptOf('և'));
        Assert.Equal(FontScript.Georgian, FontSlots.ScriptOf('ჭ'));
    }

    [Fact]
    public void Choosing_a_language_replaces_the_invoice_and_keeps_the_mode_and_options()
    {
        var input = new FontsAndLanguagesInput { Mode = DrawMode.Html, FailOnMissingCharacters = true, HeadingWeight = 800 };
        input.SellerName = "Changed";
        input.Items.Add(new LanguageInvoiceLine { Description = "extra", Quantity = 4, Rate = 10m });

        input.Apply(LanguageInvoices.Find("tamil"));

        Assert.Equal("tamil", input.Language);
        Assert.Equal("முருகன் ஜவுளி மாளிகை", input.SellerName);
        Assert.Equal(3, input.Items.Count);
        Assert.Equal(2 * 600m + 4500m + 3 * 250m, input.Total);
        Assert.Equal(DrawMode.Html, input.Mode);
        Assert.True(input.FailOnMissingCharacters);
        Assert.Equal(800, input.HeadingWeight);
        Assert.Equal("2026/0042", input.InvoiceNumber);
        Assert.Equal("hindi", new FontsAndLanguagesInput().Language);
    }

    [Fact]
    public void A_font_without_a_scripts_letters_is_reported()
    {
        var telugu = Font(Telugu + " ");

        Assert.True(FontSlots.Covers(telugu, FontScript.Telugu));
        Assert.False(FontSlots.Covers(telugu, FontScript.Devanagari));
        Assert.False(FontSlots.Covers(telugu, FontScript.Tamil));
        Assert.False(FontSlots.Covers(telugu, FontScript.Latin)); // RV-09: told apart by whether the font is embedded
        Assert.True(FontSlots.Covers(Font("Ab"), FontScript.Latin));
        Assert.True(FontSlots.Covers(Font("क"), FontScript.Devanagari)); // one letter of the script is enough
    }

    [Theory]
    [InlineData(FontScript.Bengali, 'ক')]
    [InlineData(FontScript.Gujarati, 'ક')]
    [InlineData(FontScript.Gurmukhi, 'ਕ')]
    [InlineData(FontScript.Odia, 'କ')]
    [InlineData(FontScript.Kannada, 'ಕ')]
    [InlineData(FontScript.Malayalam, 'ക')]
    public void Each_added_slot_finds_its_script_letters(FontScript script, char letter)
    {
        Assert.Equal(script, FontSlots.ScriptOf(letter));
        Assert.True(FontSlots.Covers(Font(letter.ToString()), script));
        Assert.False(FontSlots.Covers(Font(letter.ToString()), FontScript.Devanagari));
        Assert.Null(FontSlots.ScriptOf('।'));
    }

    [Fact]
    public void Every_slot_has_a_free_font_link()
    {
        Assert.All(FontSlots.Scripts, s => Assert.StartsWith("https://fonts.google.com/noto/specimen/Noto+Sans", FontSlots.FreeFontLink(s), StringComparison.Ordinal));
        Assert.Equal(12, FontSlots.Scripts.Select(FontSlots.FreeFontLink).Distinct().Count()); // Noto Sans covers Latin, Greek, and Cyrillic
        Assert.Equal("https://fonts.google.com/noto/specimen/Noto+Sans+Oriya", FontSlots.FreeFontLink(FontScript.Odia));
    }

    [Fact]
    public void A_good_font_loads_and_keeps_its_bytes()
    {
        var bytes = FontBytes(Devanagari);

        var result = UploadedFontLoader.Load("BrandDeva.ttf", bytes);

        Assert.Null(result.Error);
        Assert.Equal("BrandDeva.ttf", result.Font!.FileName);
        Assert.Same(bytes, result.Font.Bytes);
        Assert.False(result.Font.RightsAsserted);
    }

    [Theory]
    [InlineData("ttcf")]
    [InlineData("wOFF")]
    [InlineData("wOF2")]
    public void Collections_and_web_fonts_get_the_format_message(string signature)
    {
        var bytes = new byte[64];
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);

        var result = UploadedFontLoader.Load("font.bin", bytes);

        Assert.Null(result.Font);
        Assert.Equal(UploadedFontLoader.Message(PdfFontErrorReason.UnsupportedFormat), result.Error);
        Assert.Contains(".ttc", result.Error!);
        Assert.False(result.CanAssertRights);
    }

    [Fact]
    public void A_damaged_font_gets_the_damaged_message()
    {
        var result = UploadedFontLoader.Load("cut.ttf", FontBytes(Telugu)[..60]);

        Assert.Null(result.Font);
        Assert.Contains("damaged", result.Error!);
        Assert.False(result.CanAssertRights);
    }

    [Fact]
    public void A_restricted_font_fails_with_its_reason_and_loads_once_the_visitor_asserts_the_rights()
    {
        var builder = new TestFontBuilder("Restricted") { FsType = 0x0002 };
        builder.MapCharacters(Telugu);
        var bytes = builder.Build();

        var refused = UploadedFontLoader.Load("restricted.ttf", bytes);
        var allowed = UploadedFontLoader.Load("restricted.ttf", bytes, assertRights: true);

        Assert.Equal(UploadedFontLoader.Message(PdfFontErrorReason.EmbeddingRestricted), refused.Error);
        Assert.True(refused.CanAssertRights);
        Assert.Null(allowed.Error);
        Assert.True(allowed.Font!.RightsAsserted);
    }

    [Fact]
    public async Task An_upload_over_the_limit_fails_before_it_is_read()
    {
        var file = new FakeFile("huge.ttf", PdfFont.DefaultMaxFileSize + 1, Array.Empty<byte>());

        var result = await UploadedFontLoader.LoadAsync(file);

        Assert.Equal(UploadedFontLoader.Message(PdfFontErrorReason.TooLarge), result.Error);
        Assert.Contains("50 MB", result.Error!);
        Assert.False(file.Opened);
    }

    [Fact]
    public async Task An_upload_is_read_with_the_library_limit_and_loaded()
    {
        var bytes = FontBytes(Telugu);
        var file = new FakeFile("BrandTelugu.ttf", bytes.Length, bytes);

        var result = await UploadedFontLoader.LoadAsync(file);

        Assert.Null(result.Error);
        Assert.Equal(PdfFont.DefaultMaxFileSize, file.Limit); // not the browser upload's 500 KB default
        Assert.Equal(bytes, result.Font!.Bytes);
    }

    [Fact]
    public void A_variable_font_loads_again_at_another_weight_and_a_static_font_keeps_its_own()
    {
        var builder = new TestFontBuilder("Flex") { WeightAxis = (100, 400, 900) };
        builder.MapCharacters("A");
        var variable = UploadedFontLoader.Load("flex.ttf", builder.Build()).Font!;
        var still = UploadedFontLoader.Load("still.ttf", FontBytes("A")).Font!;

        Assert.Equal(700, UploadedFontLoader.AtWeight(variable, 700).Weight);
        Assert.Equal(900, UploadedFontLoader.AtWeight(variable, 1000).Weight); // clamped to the axis
        Assert.Same(variable.Font, UploadedFontLoader.AtWeight(variable, 400));
        Assert.Same(still.Font, UploadedFontLoader.AtWeight(still, 700));
    }

    #endregion

    #region Private Methods

    private static PdfFont Font(string characters) => PdfFont.FromBytes(FontBytes(characters));

    private static byte[] FontBytes(string characters)
    {
        var builder = new TestFontBuilder("Demo Test");
        builder.MapCharacters(characters);
        return builder.Build();
    }

    #endregion

    #endregion

    #region Nested Types

    // An uploaded file: records whether it was opened and with which limit.
    private sealed class FakeFile : IBrowserFile
    {
        private readonly byte[] _bytes;

        public FakeFile(string name, long size, byte[] bytes)
        {
            Name = name;
            Size = size;
            _bytes = bytes;
        }

        public string ContentType => "font/ttf";

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Limit { get; private set; }

        public string Name { get; }

        public bool Opened { get; private set; }

        public long Size { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Opened = true;
            Limit = maxAllowedSize;
            if (Size > maxAllowedSize) throw new IOException("Too large.");
            return new MemoryStream(_bytes);
        }
    }

    #endregion
}
