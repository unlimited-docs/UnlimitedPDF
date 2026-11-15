using System.Diagnostics;
using UnlimitedPDF.Models.Internal.Fonts.OpenType;
using UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

namespace UnlimitedPDF.Tests.Fonts;

/// <summary>
/// Charstring limits found at review (A06, D-013): subroutines that call each other many times without drawing are
/// stopped in CFF and CFF2 (RV-01), and CFF2 curve runs longer than CFF's 48 operands are split into valid CFF groups
/// when an instance is written (RV-05).
/// </summary>
public class CffCharstringLimitTests
{
    #region Fields and Constants

    private const byte CallSubr = 10, Return = 11, EndChar = 14, CallGsubr = 29, RMoveTo = 21;
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

    private static byte[] Instance(byte[] charstring, List<byte[]> globalSubrs)
    {
        var index = Cff2Index(globalSubrs);
        var subrs = CffIndex.Read(new FontBytes(index), 0, index.Length, "global subroutines", wideCount: true);
        return Cff2Charstring.Instance(charstring, null, subrs, null, 0, Array.Empty<double>());
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
