using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Charstring limits found at review (A06, D-013): subroutines that call each other many times without drawing are
/// stopped in CFF and CFF2, per glyph (RV-01) and across a font's glyphs (RV-18), and a subroutine call copies nothing
/// (RV-24); CFF2 blends weigh their regions once (RV-19) and an invalid vsindex is corrupt (RV-27); and CFF2 runs longer than CFF's 48 operands are split into valid CFF groups when an instance is written
/// (RV-05, RV-21).
/// </summary>
public class CffCharstringLimitTests
{
    #region Fields and Constants

    private const byte CallSubr = 10, Return = 11, EndChar = 14, CallGsubr = 29, RMoveTo = 21;
    private const byte HStem = 1, RLineTo = 5, HLineTo = 6, VLineTo = 7, RRCurveTo = 8, VsIndex = 15, Blend = 16, HintMask = 19, VStemHm = 23, RCurveLine = 24, RLineCurve = 25;
    private const byte VvCurveTo = 26, HhCurveTo = 27, VhCurveTo = 30, HvCurveTo = 31;

    #endregion

    #region Methods

    #region Public Methods

    [Fact]
    public void Cff_subroutines_that_call_each_other_many_times_without_drawing_are_corrupt()
    {
        // Nine levels each call the next twelve times: about 5e9 calls that draw nothing, from a font under 1 KB.
        var builder = new TestFontBuilder { Cff = true };
        for (int level = 0; level < 9; level++)
        {
            int next = level + 1 - 107;
            builder.CffLocalSubrs.Add(Code(Enumerable.Range(0, 12).SelectMany(_ => new object[] { next, CallSubr }).Append(Return).ToArray()));
        }
        builder.CffLocalSubrs.Add(Code(Return));
        builder.AddCharstringGlyph(500, Code(500, 0, 0, RMoveTo, -107, CallSubr, EndChar));

        var clock = Stopwatch.StartNew();
        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(builder.Build()));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("runs too long through subroutines", error.Message);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
    }

    [Fact]
    public void Cff2_subroutines_that_call_each_other_many_times_without_drawing_are_corrupt()
    {
        var subrs = new List<byte[]> { Array.Empty<byte>() };
        for (int level = 1; level <= 9; level++)
        {
            int previous = level - 1 - 107;
            subrs.Add(Code(Enumerable.Range(0, 12).SelectMany(_ => new object[] { previous, CallGsubr }).ToArray()));
        }

        var clock = Stopwatch.StartNew();
        var error = Assert.Throws<PdfFontException>(() => Instance(Code(9 - 107, CallGsubr), subrs));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("runs too long through subroutines", error.Message);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
    }

    [Fact]
    public void Cff_glyphs_each_under_the_cap_that_together_run_too_long_are_corrupt()
    {
        // RV-18: five levels each calling the next twelve times is about 814,000 steps, under one glyph's cap, but
        // loading flattens every glyph, so twenty such glyphs exceed the font's shared budget.
        var builder = new TestFontBuilder { Cff = true };
        for (int level = 0; level < 5; level++)
        {
            int next = level + 1 - 107;
            builder.CffLocalSubrs.Add(Code(Enumerable.Range(0, 12).SelectMany(_ => new object[] { next, CallSubr }).Append(Return).ToArray()));
        }
        builder.CffLocalSubrs.Add(Code(Return));
        for (int g = 0; g < 20; g++) builder.AddCharstringGlyph(500, Code(500, 0, 0, RMoveTo, -107, CallSubr, EndChar));

        var clock = Stopwatch.StartNew();
        var error = Assert.Throws<PdfFontException>(() => PdfFont.FromBytes(builder.Build()));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
        Assert.Contains("together run too long", error.Message);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
    }

    [Fact]
    public void A_long_subroutine_that_returns_at_once_costs_no_copying_per_call()
    {
        // RV-24: five levels each calling the next twelve times end in a 60,000-byte subroutine that returns at its
        // first byte; each call used to copy the whole subroutine (about 1.9 s per glyph).
        var builder = new TestFontBuilder { Cff = true };
        for (int level = 0; level < 5; level++)
        {
            int next = level + 1 - 107;
            builder.CffLocalSubrs.Add(Code(Enumerable.Range(0, 12).SelectMany(_ => new object[] { next, CallSubr }).Append(Return).ToArray()));
        }
        builder.CffLocalSubrs.Add(Code(Return).Concat(new byte[59_999]).ToArray());
        for (int g = 0; g < 3; g++) builder.AddCharstringGlyph(500, Code(500, 0, 0, RMoveTo, -107, CallSubr, EndChar));
        var bytes = builder.Build();

        var clock = Stopwatch.StartNew();
        PdfFont.FromBytes(bytes);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(70000)]
    public void An_invalid_cff2_vsindex_is_corrupt(int vsIndex)
    {
        // RV-27: a negative vsindex used to escape as IndexOutOfRangeException.
        var store = Store(regions: 1);

        var error = Assert.Throws<PdfFontException>(() => Cff2Charstring.Instance(Code(vsIndex, VsIndex, 0, Blend), null, Subrs(new List<byte[]>()), store, 0, new[] { 0.5 }));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
    }

    [Fact]
    public void Cff2_blends_weigh_their_regions_once()
    {
        // RV-19: one item variation data with 32,700 region indexes; each blend used to weigh all of them again.
        var store = Store(regions: 32700);
        var charstring = Enumerable.Range(0, 200_000).SelectMany(_ => Code(0, Blend)).ToArray();
        var subrs = Subrs(new List<byte[]>());

        var clock = Stopwatch.StartNew();
        Cff2Charstring.Instance(charstring, null, subrs, store, 0, new[] { 0.5 });

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
    }

    [Theory]
    [InlineData(HvCurveTo, 52)]
    [InlineData(VhCurveTo, 52)]
    [InlineData(HvCurveTo, 101)]
    [InlineData(VhCurveTo, 97)]
    [InlineData(HhCurveTo, 53)]
    [InlineData(HhCurveTo, 52)]
    [InlineData(VvCurveTo, 52)]
    [InlineData(VvCurveTo, 97)]
    public void Long_cff2_curve_runs_are_split_into_valid_cff_groups(byte op, int operands)
    {
        var values = Enumerable.Range(1, operands).ToArray();

        var groups = Operators(Instance(Code(values.Cast<object>().Append(op).ToArray()), new List<byte[]>()))
            .Where(g => g.Op is VvCurveTo or HhCurveTo or VhCurveTo or HvCurveTo)
            .ToList();

        Assert.True(groups.Count > 1);
        Assert.All(groups, g => Assert.True(g.Operands.Length <= 48, $"{g.Operands.Length} operands"));
        Assert.Equal(values, groups.SelectMany(g => g.Operands));
        // Every group keeps the operator: hh/vv groups hold whole curves, and hv/vh groups hold an even number of
        // curves, so each next group starts in the same direction.
        Assert.All(groups, g => Assert.Equal(op, g.Op));
        int leading = op is HhCurveTo or VvCurveTo ? operands % 4 : 0;
        int unit = op is HvCurveTo or VhCurveTo ? 8 : 4;
        Assert.Equal(leading, groups[0].Operands.Length % unit);
        Assert.All(groups.Skip(1).SkipLast(1), g => Assert.Equal(0, g.Operands.Length % unit));
    }

    [Theory]
    [InlineData(HLineTo, 60)]
    [InlineData(VLineTo, 97)]
    public void Long_cff2_line_runs_are_split_after_an_even_count(byte op, int operands)
    {
        // RV-21: an even split keeps every group starting in the same direction.
        var values = Enumerable.Range(1, operands).ToArray();

        var groups = Operators(Instance(Code(values.Cast<object>().Append(op).ToArray()), new List<byte[]>())).Where(g => g.Op == op).ToList();

        Assert.True(groups.Count > 1);
        Assert.All(groups, g => Assert.True(g.Operands.Length <= 48));
        Assert.All(groups.SkipLast(1), g => Assert.Equal(0, g.Operands.Length % 2));
        Assert.Equal(values, groups.SelectMany(g => g.Operands));
    }

    [Theory]
    [InlineData(RCurveLine, 62, RRCurveTo)]
    [InlineData(RCurveLine, 110, RRCurveTo)]
    [InlineData(RLineCurve, 66, RLineTo)]
    [InlineData(RLineCurve, 120, RLineTo)]
    public void Long_cff2_mixed_runs_write_leading_groups_on_their_own(byte op, int operands, byte leadingOp)
    {
        // RV-21: the leading curves (or lines) go out as rrcurveto (or rlineto); the last call keeps the tail.
        var values = Enumerable.Range(1, operands).ToArray();

        var groups = Operators(Instance(Code(values.Cast<object>().Append(op).ToArray()), new List<byte[]>())).Where(g => g.Op is RRCurveTo or RLineTo or RCurveLine or RLineCurve).ToList();

        Assert.All(groups.SkipLast(1), g => Assert.Equal(leadingOp, g.Op));
        Assert.Equal(op, groups[^1].Op);
        Assert.All(groups, g => Assert.True(g.Operands.Length <= 48));
        Assert.Equal(values, groups.SelectMany(g => g.Operands));
        int tail = op == RCurveLine ? 2 : 6, group = op == RCurveLine ? 6 : 2;
        Assert.Equal(0, (groups[^1].Operands.Length - tail) % group);
        Assert.True(groups[^1].Operands.Length > tail);
    }

    [Fact]
    public void Long_cff2_stem_runs_start_each_later_group_from_the_absolute_edge()
    {
        // RV-21: 30 stems (60 operands, each edge relative to the previous one); a stem operator measures its first
        // edge from zero, so the second group's first edge is the sum of everything before it plus its own delta.
        var values = Enumerable.Range(1, 60).ToArray();

        var groups = Operators(Instance(Code(values.Cast<object>().Append(HStem).Concat(new object[] { 0, 0, RMoveTo }).ToArray()), new List<byte[]>())).Where(g => g.Op == HStem).ToList();

        Assert.Equal(2, groups.Count);
        Assert.Equal(values.Take(48), groups[0].Operands);
        Assert.Equal(values.Take(49).Sum(), groups[1].Operands[0]);
        Assert.Equal(values.Skip(49), groups[1].Operands.Skip(1));
    }

    [Fact]
    public void More_than_24_implicit_stems_before_a_hint_mask_are_written_as_vstemhm()
    {
        var values = Enumerable.Range(1, 60).ToArray();
        var charstring = Code(values.Cast<object>().Append(HintMask).ToArray()).Concat(new byte[] { 139, 139, 139, 139 }).ToArray();

        var operators = Operators(Instance(charstring, new List<byte[]>()));

        Assert.Equal(new[] { (int)VStemHm, VStemHm, HintMask }, operators.Take(3).Select(o => o.Op));
        Assert.Empty(operators[2].Operands);
        Assert.Equal(30, operators.Take(2).Sum(o => o.Operands.Length) / 2);
    }

    [Theory]
    [InlineData(HvCurveTo, 50)]
    [InlineData(HhCurveTo, 51)]
    public void Cff2_curve_runs_with_the_wrong_operand_count_are_corrupt(byte op, int operands)
    {
        var error = Assert.Throws<PdfFontException>(() => Instance(Code(Enumerable.Repeat((object)10, operands).Append(op).ToArray()), new List<byte[]>()));

        Assert.Equal(PdfFontErrorReason.CorruptData, error.Reason);
    }

    #endregion

    #region Private Methods

    // Numbers become charstring operands; values passed as byte become operators.
    private static byte[] Code(params object[] parts) => parts.SelectMany(p => p switch
    {
        byte op => new[] { op },
        int number => TestFontBuilder.CharstringNumber(number),
        _ => throw new ArgumentException("Unexpected part.")
    }).ToArray();

    private static byte[] Instance(byte[] charstring, List<byte[]> globalSubrs) =>
        Cff2Charstring.Instance(charstring, null, Subrs(globalSubrs), null, 0, Array.Empty<double>());

    // An item variation store with one axis, one region from 0 to 1, and one data with `regions` region indexes.
    private static ItemVariationStore Store(int regions)
    {
        var w = new BigEndianWriter().U16(1).U32(12).U16(1).U32(12 + 4 + 6);
        w.U16(1).U16(1).U16(0).U16(16384).U16(16384);
        w.U16(0).U16(0).U16((ushort)regions);
        for (int i = 0; i < regions; i++) w.U16(0);
        var bytes = w.ToArray();
        return ItemVariationStore.Read(new FontBytes(bytes), 0, bytes.Length, 1);
    }

    private static CffIndex Subrs(List<byte[]> globalSubrs)
    {
        var index = Cff2Index(globalSubrs);
        return CffIndex.Read(new FontBytes(index), 0, index.Length, "global subroutines", wideCount: true);
    }

    private static byte[] Cff2Index(IReadOnlyList<byte[]> entries)
    {
        var w = new BigEndianWriter().U32((uint)entries.Count);
        if (entries.Count == 0) return w.ToArray();
        w.U8(4);
        uint offset = 1;
        w.U32(offset);
        foreach (var entry in entries) w.U32(offset += (uint)entry.Length);
        foreach (var entry in entries) w.Bytes(entry);
        return w.ToArray();
    }

    // Reads a CFF charstring into operators with the integer operands before each.
    private static List<(int Op, int[] Operands)> Operators(byte[] code)
    {
        var result = new List<(int, int[])>();
        var operands = new List<int>();
        int p = 0;
        while (p < code.Length)
        {
            int b0 = code[p];
            if (b0 >= 32 && b0 <= 246) { operands.Add(b0 - 139); p++; }
            else if (b0 >= 247 && b0 <= 250) { operands.Add((b0 - 247) * 256 + code[p + 1] + 108); p += 2; }
            else if (b0 >= 251 && b0 <= 254) { operands.Add(-(b0 - 251) * 256 - code[p + 1] - 108); p += 2; }
            else if (b0 == 28) { operands.Add((short)(code[p + 1] << 8 | code[p + 2])); p += 3; }
            else if (b0 == 255) { operands.Add((code[p + 1] << 24 | code[p + 2] << 16 | code[p + 3] << 8 | code[p + 4]) >> 16); p += 5; }
            else
            {
                int op = b0 == 12 ? 1200 + code[p + 1] : b0;
                p += b0 == 12 ? 2 : 1;
                result.Add((op, operands.ToArray()));
                operands.Clear();
            }
        }
        return result;
    }

    #endregion

    #endregion
}
