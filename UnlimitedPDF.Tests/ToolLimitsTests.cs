using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Services;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The free tools refuse a file over a limit, or one they can't read, with a message naming the reason, before any work
/// starts, and keep the files already added; the scenarios keep their own limits (26112201 AC-08, AC-10; OWASP A06, A10).
/// </summary>
public class ToolLimitsTests
{
    #region Fields and Constants

    private const string StandardEncryption = "<< /Filter /Standard /V 2 /R 3 /Length 128 /O <00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF> /U <FFEEDDCCBBAA99887766554433221100FFEEDDCCBBAA99887766554433221100> /P -4 >>";

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public async Task A_readable_pdf_within_the_limit_loads()
    {
        var (file, error) = await UploadedPdfLoader.LoadAsync(new FakeFile("five.pdf", ReaderFixtureBuilder.DocumentBytes(5)), ToolLimits.MaxPdfBytes);

        Assert.Null(error);
        Assert.Equal(5, file!.PageCount);
    }

    [Fact]
    public async Task A_pdf_over_50_MB_is_refused_before_it_is_read()
    {
        var big = new FakeFile("big.pdf", [], declaredSize: ToolLimits.MaxPdfBytes + 1);

        var (file, error) = await UploadedPdfLoader.LoadAsync(big, ToolLimits.MaxPdfBytes);

        Assert.Null(file);
        Assert.Equal("The file is larger than 50 MB.", error);
        Assert.False(big.Opened);
    }

    [Fact]
    public async Task The_scenarios_keep_their_20_MB_limit_and_messages()
    {
        var (_, error) = await UploadedPdfLoader.LoadAsync(new FakeFile("big.pdf", [], declaredSize: UploadedPdfLoader.MaxFileBytes + 1));
        Assert.Equal("The file is larger than 20 MB.", error);

        var (_, encrypted) = await UploadedPdfLoader.LoadAsync(new FakeFile("locked.pdf", EncryptedPdf()));
        Assert.Equal("This PDF is encrypted, so it can't be merged.", encrypted);
    }

    [Theory]
    [MemberData(nameof(UnreadablePdfs))]
    public async Task An_unreadable_pdf_is_refused_with_its_reason(byte[] bytes, string expected)
    {
        var (file, error) = await UploadedPdfLoader.LoadAsync(new FakeFile("file.pdf", bytes), ToolLimits.MaxPdfBytes);

        Assert.Null(file);
        Assert.Equal(expected, error);
    }

    [Fact]
    public async Task A_48_megapixel_jpeg_photo_loads_and_a_60_megapixel_one_is_refused()
    {
        var photo = await LoadPicture(new JpegBuilder { Width = 8000, Height = 6000 }.Build());
        var huge = await LoadPicture(new JpegBuilder { Width = 10000, Height = 6000 }.Build());

        Assert.Null(photo.Error);
        Assert.NotNull(photo.Image);
        Assert.Equal("The JPEG is larger than 50 megapixels.", huge.Error);
    }

    [Fact]
    public async Task A_png_over_4096_by_4096_is_refused()
    {
        var small = await LoadPicture(new PngBuilder { Width = 8, Height = 8 }.Build());
        var big = await LoadPicture(new PngBuilder { Width = 5000, Height = 4000, CompressedOverride = [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01] }.Build());

        Assert.Null(small.Error);
        Assert.Equal("The PNG is larger than 4096 x 4096 pixels.", big.Error);
    }

    [Fact]
    public async Task A_picture_over_10_MB_or_not_a_picture_is_refused()
    {
        var big = await UploadedImageLoader.LoadAsync(new FakeFile("big.jpg", [], declaredSize: ToolLimits.MaxPictureBytes + 1), ToolLimits.MaxPictureBytes, ToolLimits.MaxJpegPixels, ToolLimits.MaxPngPixels);
        var text = await LoadPicture("not a picture"u8.ToArray());

        Assert.Equal("The file is larger than 10 MB.", big.Error);
        Assert.Equal("This file isn't a JPEG or PNG image.", text.Error);
    }

    [Fact]
    public async Task The_scenarios_keep_their_picture_limits()
    {
        var jpeg = await UploadedImageLoader.LoadAsync(new FakeFile("photo.jpg", new JpegBuilder { Width = 5000, Height = 4000 }.Build()));

        Assert.Equal("The image is larger than 4096 x 4096 pixels.", jpeg.Error);
    }

    [Fact]
    public void A_file_set_refuses_the_21st_file_and_keeps_the_others()
    {
        var set = new ToolFileSet<int>();
        for (int i = 1; i <= ToolLimits.MaxFiles; i++)
        {
            Assert.Null(set.CheckRoomFor(1));
            set.Add($"f{i}.pdf", 1, i);
        }

        Assert.Equal("One run takes up to 20 files.", set.CheckRoomFor(1));
        Assert.Throws<InvalidOperationException>(() => set.Add("f21.pdf", 1, 21));
        Assert.Equal(20, set.Count);
    }

    [Fact]
    public void A_file_set_refuses_a_file_that_takes_it_over_200_MB()
    {
        var set = new ToolFileSet<int>();
        set.Add("a.pdf", 50 * ToolLimits.Megabyte, 1);
        set.Add("b.pdf", 50 * ToolLimits.Megabyte, 2);
        set.Add("c.pdf", 50 * ToolLimits.Megabyte, 3);
        set.Add("d.pdf", 50 * ToolLimits.Megabyte, 4);

        Assert.Equal("These files add up to 250 MB. One run takes up to 200 MB in total.", set.CheckRoomFor(50 * ToolLimits.Megabyte));
        Assert.Null(new ToolFileSet<int>().CheckRoomFor(ToolLimits.MaxTotalBytes));
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void A_file_set_reorders_removes_and_sorts()
    {
        var set = new ToolFileSet<string>();
        foreach (var name in new[] { "c.pdf", "A.pdf", "b.pdf" }) set.Add(name, 1, name);

        set.MoveEarlier(0);
        set.MoveLater(2);
        Assert.Equal(["c.pdf", "A.pdf", "b.pdf"], set.Files.Select(f => f.Name));

        set.MoveLater(0);
        Assert.Equal(["A.pdf", "c.pdf", "b.pdf"], set.Files.Select(f => f.Name));

        set.Move(2, 0);
        Assert.Equal(["b.pdf", "A.pdf", "c.pdf"], set.Files.Select(f => f.Name));

        set.SortByName();
        Assert.Equal(["A.pdf", "b.pdf", "c.pdf"], set.Files.Select(f => f.Name));

        set.RemoveAt(1);
        Assert.Equal(["A.pdf", "c.pdf"], set.Files.Select(f => f.Name));
        Assert.Equal(2, set.TotalBytes);
        Assert.Equal(2, set.Files.Select(f => f.Id).Distinct().Count());
    }

    #endregion

    #region Private Methods

    private static byte[] EncryptedPdf() => new ReaderFixtureBuilder()
        .AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .AddObject(2, "<< /Type /Pages /Kids [] /Count 0 >>")
        .AddObject(3, "(three)")
        .AddObject(4, StandardEncryption)
        .ToBytes(trailerEntries: "/Root 1 0 R /Encrypt 4 0 R /ID [<0011> <0011>]");

    private static Task<UploadResult> LoadPicture(byte[] bytes) =>
        UploadedImageLoader.LoadAsync(new FakeFile("picture", bytes), ToolLimits.MaxPictureBytes, ToolLimits.MaxJpegPixels, ToolLimits.MaxPngPixels);

    #endregion

    #endregion

    #region Properties, Indexers

    public static TheoryData<byte[], string> UnreadablePdfs => new()
    {
        { EncryptedPdf(), "This PDF is encrypted, so it can't be opened here." },
        { "not a pdf at all"u8.ToArray(), "This file isn't a readable PDF." },
        { ReaderFixtureBuilder.DocumentBytes(0), "This PDF has no pages." },
    };

    #endregion

    #region Nested Types

    /// <summary>
    /// A picked file read from bytes, as the browser hands one over.
    /// </summary>
    private sealed class FakeFile(string name, byte[] bytes, long? declaredSize = null) : IBrowserFile
    {
        public string ContentType => "application/octet-stream";

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public string Name => name;

        public bool Opened { get; private set; }

        public long Size => declaredSize ?? bytes.Length;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Opened = true;
            if (Size > maxAllowedSize)
            {
                throw new IOException(string.Create(CultureInfo.InvariantCulture, $"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes."));
            }

            return new MemoryStream(bytes, writable: false);
        }
    }

    #endregion
}
