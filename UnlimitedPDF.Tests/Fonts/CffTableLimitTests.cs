using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Limits on the parts of a CFF or CFF2 table that Font DICTs share, found at review (A06, RV-33): a Private DICT
/// that many Font DICTs name is parsed once, a DICT may be at most 64 KB, and Private DICTs, Local Subr INDEXes, and
/// item variation data that overlap (region indexes or delta rows) are corrupt; a variation store works out each delta
/// once for given coordinates, shared by every outer index that names the same data.
/// </summary>
public class CffTableLimitTests
{
    #region Fields and Constants

    private const byte CallSubr = 10, Return = 11, EndChar = 14;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Font_dicts_that_share_one_large_private_dict_parse_it_once()
    {
        // RV-33: 256 Font DICTs name one Private DICT of 30,000 operators; each used to parse it again, allocating an
        // array per operator (about 250 MB here, and 31 GB for a 7.8 MB table).
        var filler = Filler(60_000);
        var table = CidCff(256, Enumerable.Repeat((filler.Length + 6, 0), 256).ToList(),
            filler.Concat(DictInt(filler.Length + 6)).Append((byte)19).Concat(Index(new[] { new[] { Return } })).ToArray());

        long before = GC.GetAllocatedBytesForCurrentThread();
        var font = CffFont.Read(new FontBytes(table), 0, table.Length, 256);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated / (1024 * 1024)} MB allocated");
        Assert.Equal(new[] { EndChar }, font.Flatten(255)[^1..]);
    }

    [Fact]
    public void Cff2_font_dicts_that_share_one_large_private_dict_parse_it_once()
    {
        var filler = Filler(60_000);
        var table = Cff2(256, Enumerable.Repeat((filler.Length, 0), 256).ToList(), filler);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Cff2Font.Read(new FontBytes(table), 0, table.Length, 256, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated / (1024 * 1024)} MB allocated");
    }

    [Fact]
    public void A_cff_dict_over_64_kb_is_corrupt()
    {
        var filler = Filler(70_000);
        var table = CidCff(1, new[] { (filler.Length, 0) }, filler);

        var error = Assert.Throws<PdfFontException>(() => CffFont.Read(new FontBytes(table), 0, table.Length, 1));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("implausibly large CFF DICT", error.Message);
    }

    [Fact]
    public void Font_dicts_that_name_overlapping_private_dicts_are_corrupt()
    {
        // Each Font DICT names a Private DICT one byte further on, so no two are the same and reuse cannot help.
        var table = CidCff(256, Enumerable.Range(0, 256).Select(fd => (59_000, fd)).ToList(), Filler(60_000));

        var error = Assert.Throws<PdfFontException>(() => CffFont.Read(new FontBytes(table), 0, table.Length, 256));

        Assert.Contains("Private DICTs that overlap", error.Message);
    }

    [Fact]
    public void Cff2_font_dicts_that_name_overlapping_private_dicts_are_corrupt()
    {
        var table = Cff2(256, Enumerable.Range(0, 256).Select(fd => (59_000, fd)).ToList(), Filler(60_000));

        var error = Assert.Throws<PdfFontException>(() => Cff2Font.Read(new FontBytes(table), 0, table.Length, 256, 0));

        Assert.Contains("Private DICTs that overlap", error.Message);
    }

    [Fact]
    public void Private_dicts_that_name_overlapping_subroutine_indexes_are_corrupt()
    {
        // Every start in a run of 0x01 bytes reads as an INDEX of 257 empty subroutines; Private DICT fd names the
        // one that starts fd bytes into the run.
        const int fontDicts = 256, privateSize = 6, run = fontDicts * privateSize;
        var privates = Enumerable.Range(0, fontDicts).SelectMany(fd => DictInt(run + fd - fd * privateSize).Append((byte)19)).ToArray();
        var table = CidCff(fontDicts, Enumerable.Range(0, fontDicts).Select(fd => (privateSize, fd * privateSize)).ToList(),
            privates.Concat(Enumerable.Repeat((byte)1, 600)).ToArray());

        var error = Assert.Throws<PdfFontException>(() => CffFont.Read(new FontBytes(table), 0, table.Length, fontDicts));

        Assert.Contains("Local Subr INDEXes that overlap", error.Message);
    }

    [Fact]
    public void Item_variation_data_named_many_times_is_read_once()
    {
        // 16,000 entries name one ItemVariationData of 16,000 region indexes; each used to read its own copy (about
        // 1 GB allocated from a 100 KB store).
        const int entries = 16_000, regionIndexes = 16_000;
        int data = 8 + 4 * entries, regions = data + 6 + 2 * regionIndexes;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16(entries);
        for (int i = 0; i < entries; i++) w.U32((uint)data);
        w.U16(0).U16(0).U16(regionIndexes);
        for (int r = 0; r < regionIndexes; r++) w.U16(0);
        w.U16(1).U16(1).U16(0).U16(16384).U16(16384);
        var store = w.ToArray();

        long before = GC.GetAllocatedBytesForCurrentThread();
        ItemVariationStore.Read(new FontBytes(store), 0, store.Length, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 16 * 1024 * 1024, $"{allocated / (1024 * 1024)} MB allocated");
    }

    [Fact]
    public void Overlapping_item_variation_data_is_corrupt()
    {
        // A run of the value 100 reads, from every even start, as an ItemVariationData of 100 items over 100 regions,
        // so 1,000 entries one value apart name distinct data that share most of their bytes.
        const int entries = 1_000, value = 100;
        int data = 8 + 4 * entries, runValues = entries + 3 + value + value * value, regions = data + 2 * runValues;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16(entries);
        for (int i = 0; i < entries; i++) w.U32((uint)(data + 2 * i));
        for (int i = 0; i < runValues; i++) w.U16(value);
        w.U16(1).U16(value + 1);
        for (int r = 0; r <= value; r++) w.U16(0).U16(0).U16(0);
        var store = w.ToArray();

        var error = Assert.Throws<PdfFontException>(() => ItemVariationStore.Read(new FontBytes(store), 0, store.Length, 1));

        Assert.Contains("item variation data that overlap", error.Message);
    }

    [Fact]
    public void Item_variation_data_whose_delta_rows_overlap_is_corrupt()
    {
        // RV-34: each of 50 ItemVariationData (100 items over 100 regions) starts where the previous one's delta rows
        // start, so their region indexes never overlap but their rows do; every distinct (outer, inner) a glyph names
        // then costs a row read of its own (about 90 times the store's bytes with large regions).
        const int count = 50, items = 100, regionIndexes = 100, block = 6 + 2 * regionIndexes;
        int data = 8 + 4 * count, regions = data + count * block + items * regionIndexes;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16(count);
        for (int i = 0; i < count; i++) w.U32((uint)(data + i * block));
        for (int i = 0; i < count; i++)
        {
            w.U16(items).U16(0).U16(regionIndexes);
            for (int r = 0; r < regionIndexes; r++) w.U16(0);
        }
        w.Bytes(new byte[items * regionIndexes]);
        w.U16(1).U16(1).U16(0).U16(16384).U16(16384);
        var store = w.ToArray();

        var error = Assert.Throws<PdfFontException>(() => ItemVariationStore.Read(new FontBytes(store), 0, store.Length, 1));

        Assert.Contains("item variation data that overlap", error.Message);
    }

    [Fact]
    public void Region_scalars_are_worked_out_once_per_region()
    {
        // RV-36: 85 delta sets of 65,535 region indexes all name one region over 64 axes; each index used to weigh the
        // region over every axis again (356 million axis steps, about 3 s, from this 16.7 MB store).
        const int sets = 85, regionIndexes = 65_535, axes = 64;
        int block = 6 + 2 * regionIndexes + regionIndexes, data = 8 + 4 * sets, regions = data + sets * block;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16(sets);
        for (int i = 0; i < sets; i++) w.U32((uint)(data + i * block));
        var row = Enumerable.Repeat((byte)1, regionIndexes).ToArray();
        for (int i = 0; i < sets; i++)
        {
            w.U16(1).U16(0).U16(regionIndexes);
            w.Bytes(new byte[2 * regionIndexes]);
            w.Bytes(row);
        }
        w.U16(axes).U16(1);
        for (int a = 0; a < axes; a++) w.U16(0).U16(16384).U16(16384);
        var bytes = w.ToArray();
        var fontBytes = new FontBytes(bytes);
        var store = ItemVariationStore.Read(fontBytes, 0, bytes.Length, axes);
        var coordinates = Enumerable.Repeat(0.5, axes).ToArray();
        var clock = Stopwatch.StartNew();

        double total = 0;
        for (int outer = 0; outer < sets; outer++) total += store.Delta(fontBytes, outer, 0, coordinates);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), clock.Elapsed.ToString());
        Assert.Equal(sets * regionIndexes * Math.Pow(0.5, axes), total, 1e-20);
    }

    [Fact]
    public void A_delta_set_that_every_glyph_names_is_worked_out_once()
    {
        // Glyphs past the end of an HVAR map all use its last entry: 65,535 glyphs naming one delta set over 30,000
        // regions used to cost about two billion steps and 15 GB of scalar arrays.
        const int regionCount = 30_000;
        int regions = 8 + 4, data = regions + 4 + 6 * regionCount;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16(1).U32((uint)data);
        w.U16(1).U16(regionCount);
        for (int r = 0; r < regionCount; r++) w.U16(0).U16(16384).U16(16384);
        w.U16(1).U16(0).U16(regionCount);
        for (int r = 0; r < regionCount; r++) w.U16((ushort)r);
        for (int r = 0; r < regionCount; r++) w.U8(1);
        var bytes = w.ToArray();
        var fontBytes = new FontBytes(bytes);
        var store = ItemVariationStore.Read(fontBytes, 0, bytes.Length, 1);
        var coordinates = new[] { 1.0 };
        var clock = Stopwatch.StartNew();

        double delta = 0;
        for (int glyph = 0; glyph < 65_535; glyph++) delta = store.Delta(fontBytes, 0, 0, coordinates);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), clock.Elapsed.ToString());
        Assert.Equal(regionCount, delta);
    }

    [Fact]
    public void Outer_indexes_that_share_one_variation_data_share_its_results()
    {
        // RV-38: an HVAR map can send each glyph to another outer index, all naming one ItemVariationData of 65,535
        // region indexes; each outer used to build its own scalars and read its own row (about 1 GB kept here, and
        // about 32 GB from a 460 KB store with 65,535 outers).
        const int outers = 2_048, regionIndexes = 65_535;
        var bytes = SharedDataStore(outers, regionIndexes, items: 1);
        var fontBytes = new FontBytes(bytes);
        var store = ItemVariationStore.Read(fontBytes, 0, bytes.Length, 1);
        var coordinates = new[] { 0.5 };

        long before = GC.GetAllocatedBytesForCurrentThread();
        double total = 0;
        for (int outer = 0; outer < outers; outer++) total += store.Delta(fontBytes, outer, 0, coordinates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 16 * 1024 * 1024, $"{allocated / (1024 * 1024)} MB allocated");
        Assert.Equal(outers * regionIndexes * 0.5, total);
    }

    [Fact]
    public void Cff2_blends_over_outer_indexes_that_share_one_variation_data_share_its_scalars()
    {
        // RV-38: vsindex o, then an empty blend, for 4,000 outer indexes naming one ItemVariationData of 16,000 region
        // indexes; each vsindex used to build its own scalars (about 500 MB).
        const int outers = 4_000, regionIndexes = 16_000;
        var bytes = SharedDataStore(outers, regionIndexes, items: 0);
        var store = ItemVariationStore.Read(new FontBytes(bytes), 0, bytes.Length, 1);
        var charstring = Enumerable.Range(0, outers).SelectMany(o => new byte[] { 28, (byte)(o >> 8), (byte)o, 15, 139, 16 }).ToArray();
        var globalSubrs = CffIndex.Read(new FontBytes(new byte[4]), 0, 4, "Global Subr INDEX", wideCount: true);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Cff2Charstring.Instance(charstring, null, globalSubrs, store, 0, new[] { 0.5 });
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 16 * 1024 * 1024, $"{allocated / (1024 * 1024)} MB allocated");
    }

    [Fact]
    public void Cff2_blends_over_variation_data_without_regions_cost_no_more_than_their_steps()
    {
        // RV-40: with no regions a blend of 512 values removes nothing, so "512 blend" can repeat on the same values;
        // each repeat paid 2 steps but passed over all 512 values (2.8 s for one glyph at the step cap).
        var bytes = SharedDataStore(outers: 1, regionIndexes: 0, items: 0);
        var store = ItemVariationStore.Read(new FontBytes(bytes), 0, bytes.Length, 1);
        byte[] count = { 28, 2, 0 }; // 512
        var charstring = Enumerable.Repeat((byte)139, 512) // 512 zeros
            .Concat(Enumerable.Range(0, 400_000).SelectMany(_ => count.Append((byte)16)))
            .Append((byte)5) // rlineto takes the 512 values
            .ToArray();
        var globalSubrs = CffIndex.Read(new FontBytes(new byte[4]), 0, 4, "Global Subr INDEX", wideCount: true);
        var clock = Stopwatch.StartNew();

        var result = Cff2Charstring.Instance(charstring, null, globalSubrs, store, 0, new[] { 0.5 });

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), clock.Elapsed.ToString());
        Assert.Equal(EndChar, result[^1]);
    }

    [Fact]
    public void Cff_glyphs_that_each_write_many_large_hint_masks_are_corrupt()
    {
        // RV-43: one subroutine declares 480 stems and writes 500 hint masks of 60 bytes; 4,000 glyphs of three bytes
        // each call it, so a 65 KB table flattens to 126 MB, which an instance keeps (2.5 GB for 65,535 such glyphs).
        var subroutine = Enumerable.Range(0, 20).SelectMany(_ => Enumerable.Repeat((byte)139, 48).Append((byte)18)) // hstemhm
            .Concat(Enumerable.Range(0, 500).SelectMany(_ => new byte[] { 19 }.Concat(Enumerable.Repeat((byte)0xFF, 60))))
            .Append(Return).ToArray();
        var table = CidCff(4_000, new[] { (6, 0) }, DictInt(6).Append((byte)19).Concat(Index(new[] { subroutine })).ToArray());

        var error = Assert.Throws<PdfFontException>(() => CffFont.Read(new FontBytes(table), 0, table.Length, 4_000));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
    }

    [Fact]
    public void Cff2_glyphs_that_each_write_many_large_hint_masks_are_corrupt()
    {
        // RV-43, CFF2: 480 stems and 1,000 hint masks of 60 bytes per glyph, sharing one budget for a 1 KB table.
        var charstring = Enumerable.Range(0, 2).SelectMany(_ => Enumerable.Repeat((byte)139, 480).Append((byte)1)) // hstem
            .Concat(Enumerable.Range(0, 1_000).SelectMany(_ => new byte[] { 19 }.Concat(Enumerable.Repeat((byte)0xFF, 60))))
            .ToArray();
        var globalSubrs = CffIndex.Read(new FontBytes(new byte[4]), 0, 4, "Global Subr INDEX", wideCount: true);
        var work = new CharstringWork(2_000, 1_024);

        var error = Assert.Throws<PdfFontException>(() =>
        {
            for (int glyph = 0; glyph < 2_000; glyph++) Cff2Charstring.Instance(charstring, null, globalSubrs, null, 0, Array.Empty<double>(), work);
        });

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
    }

    [Fact]
    public void Flattened_glyphs_that_together_outgrow_the_table_are_corrupt()
    {
        // RV-43: the output budget is 32 MB plus 8 bytes per table byte (40 MB for a 1 MB table); glyphs of 5-byte
        // operands write about 5 bytes per step, so the step budget alone would allow about 340 MB.
        var charstring = Enumerable.Range(0, 250).SelectMany(_ => Enumerable.Range(0, 48).SelectMany(_ => new byte[] { 255, 0, 1, 0, 0 }).Append((byte)5))
            .Append(EndChar).ToArray();
        var globalSubrs = CffIndex.Read(new FontBytes(new byte[2]), 0, 2, "Global Subr INDEX");
        var work = new CharstringWork(65_535, 1_000_000);

        var error = Assert.Throws<PdfFontException>(() =>
        {
            for (int glyph = 0; glyph < 1_000; glyph++) Type2Charstring.Flatten(charstring, null, globalSubrs, work);
        });

        Assert.Contains("expand too far", error.Message);
    }

    #endregion

    #region Private Methods

    // Private DICT filler: BlueValues with one operand, repeated; read from any byte it still parses.
    private static byte[] Filler(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 2 == 0 ? 139 : 1)).ToArray();

    // An item variation store with one axis and one region (0 to 1, peak 1) whose `outers` entries all name one
    // ItemVariationData of `regionIndexes` indexes to that region and `items` rows of deltas of 1.
    private static byte[] SharedDataStore(int outers, int regionIndexes, int items)
    {
        int regions = 8 + 4 * outers, data = regions + 4 + 6;
        var w = new BigEndianWriter().U16(1).U32((uint)regions).U16((ushort)outers);
        for (int i = 0; i < outers; i++) w.U32((uint)data);
        w.U16(1).U16(1).U16(0).U16(16384).U16(16384);
        w.U16((ushort)items).U16(0).U16((ushort)regionIndexes);
        w.Bytes(new byte[2 * regionIndexes]);
        w.Bytes(Enumerable.Repeat((byte)1, items * regionIndexes).ToArray());
        return w.ToArray();
    }

    private static byte[] DictInt(int v) => new byte[] { 29, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    private static byte[] Index(IReadOnlyList<byte[]> items, bool wideCount = false)
    {
        var w = wideCount ? new BigEndianWriter().U32((uint)items.Count) : new BigEndianWriter().U16((ushort)items.Count);
        if (items.Count == 0) return w.ToArray();
        w.U8(4);
        uint offset = 1;
        w.U32(offset);
        foreach (var item in items) w.U32(offset += (uint)item.Length);
        foreach (var item in items) w.Bytes(item);
        return w.ToArray();
    }

    // Font DICT fd names Private DICT privates[fd] (its size, and its offset into the tail); glyph g uses Font DICT
    // g mod the Font DICT count and calls subroutine -107 (biased) once. The tail ends the table.
    private static byte[] FontDicts(IReadOnlyList<(int Size, int Offset)> privates, int tailStart, bool wideCount) =>
        Index(privates.Select(p => DictInt(p.Size).Concat(DictInt(tailStart + p.Offset)).Append((byte)18).ToArray()).ToList(), wideCount);

    // A CID-keyed CFF table with the given Font DICTs.
    private static byte[] CidCff(int glyphs, IReadOnlyList<(int Size, int Offset)> privates, byte[] tail)
    {
        var header = new byte[] { 1, 0, 4, 4 };
        var name = Index(new[] { Encoding.ASCII.GetBytes("A") });
        var strings = Index(new[] { Encoding.ASCII.GetBytes("Adobe"), Encoding.ASCII.GetBytes("Identity") });
        var global = Index(Array.Empty<byte[]>());
        const int topSize = 7 + 7 + 7 + 6;
        int topIndexSize = 2 + 1 + 2 * 4 + topSize;
        int fdSelect = header.Length + name.Length + topIndexSize + strings.Length + global.Length;
        var fdSelectBytes = new byte[] { 0 }.Concat(Enumerable.Range(0, glyphs).Select(g => (byte)(g % privates.Count))).ToArray();
        int fdArray = fdSelect + fdSelectBytes.Length;
        int charStrings = fdArray + FontDicts(privates, 0, false).Length;
        var charStringIndex = Index(Enumerable.Range(0, glyphs).Select(_ => new byte[] { 32, CallSubr, EndChar }).ToList());
        int tailStart = charStrings + charStringIndex.Length;

        var top = new List<byte> { 248, 27, 248, 28, 139, 12, 30 }; // ROS: Adobe Identity 0
        top.AddRange(DictInt(fdArray)); top.AddRange(new byte[] { 12, 36 });
        top.AddRange(DictInt(fdSelect)); top.AddRange(new byte[] { 12, 37 });
        top.AddRange(DictInt(charStrings)); top.Add(17);
        return header.Concat(name).Concat(Index(new[] { top.ToArray() })).Concat(strings).Concat(global)
            .Concat(fdSelectBytes).Concat(FontDicts(privates, tailStart, false)).Concat(charStringIndex).Concat(tail)
            .ToArray();
    }

    // A CFF2 table with the given Font DICTs, FDSelect format 0, and glyphs that only end.
    private static byte[] Cff2(int glyphs, IReadOnlyList<(int Size, int Offset)> privates, byte[] tail)
    {
        const int topSize = 6 + 7 + 7;
        int global = 5 + topSize;
        int fdSelect = global + 4;
        var fdSelectBytes = new byte[] { 0 }.Concat(Enumerable.Range(0, glyphs).Select(g => (byte)(g % privates.Count))).ToArray();
        int fdArray = fdSelect + fdSelectBytes.Length;
        int charStrings = fdArray + FontDicts(privates, 0, true).Length;
        var charStringIndex = Index(Enumerable.Range(0, glyphs).Select(_ => Array.Empty<byte>()).ToList(), wideCount: true);
        int tailStart = charStrings + charStringIndex.Length;

        var top = new List<byte>();
        top.AddRange(DictInt(charStrings)); top.Add(17);
        top.AddRange(DictInt(fdArray)); top.AddRange(new byte[] { 12, 36 });
        top.AddRange(DictInt(fdSelect)); top.AddRange(new byte[] { 12, 37 });
        return new byte[] { 2, 0, 5, 0, topSize }.Concat(top).Concat(new byte[4])
            .Concat(fdSelectBytes).Concat(FontDicts(privates, tailStart, true)).Concat(charStringIndex).Concat(tail)
            .ToArray();
    }

    #endregion

    #endregion
}
