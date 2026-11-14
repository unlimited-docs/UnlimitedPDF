namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// A parsed CFF DICT: operators (12 x is stored as 1200 + x) mapped to their operands.
/// </summary>
internal sealed class CffDict
{
    #region Fields and Constants

    // A DICT entry has a handful of operands; the CFF2 operand stack limit is the most any can need.
    private const int MaxOperands = 513;

    private readonly Dictionary<int, double[]> _entries = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Parses the DICT data between <paramref name="offset"/> and <paramref name="offset"/> + <paramref name="length"/>.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The DICT's length.</param>
    /// <returns>The DICT.</returns>
    public static CffDict Read(FontBytes data, int offset, int length)
    {
        data.Require(offset, length, "a CFF DICT");
        var dict = new CffDict();
        var operands = new List<double>();
        int p = offset, end = offset + length;
        while (p < end)
        {
            int b0 = data.U8(p);
            if (b0 <= 24) // operators; 22 to 24 (vsindex, blend, vstore) occur only in CFF2
            {
                int op = b0;
                p++;
                if (b0 == 12)
                {
                    if (p >= end) throw FontBytes.Corrupt("has a truncated CFF DICT operator");
                    op = 1200 + data.U8(p++);
                }
                dict._entries[op] = operands.ToArray();
                operands.Clear();
                continue;
            }

            if (operands.Count >= MaxOperands) throw FontBytes.Corrupt("has a CFF DICT with too many operands");
            if (b0 == 28)
            {
                if (p + 3 > end) throw Truncated();
                operands.Add(data.I16(p + 1));
                p += 3;
            }
            else if (b0 == 29)
            {
                if (p + 5 > end) throw Truncated();
                operands.Add(data.I32(p + 1));
                p += 5;
            }
            else if (b0 == 30)
            {
                p = ReadReal(data, p + 1, end, out double value);
                operands.Add(value);
            }
            else if (b0 >= 32 && b0 <= 246)
            {
                operands.Add(b0 - 139);
                p++;
            }
            else if (b0 >= 247 && b0 <= 250)
            {
                if (p + 2 > end) throw Truncated();
                operands.Add((b0 - 247) * 256 + data.U8(p + 1) + 108);
                p += 2;
            }
            else if (b0 >= 251 && b0 <= 254)
            {
                if (p + 2 > end) throw Truncated();
                operands.Add(-(b0 - 251) * 256 - data.U8(p + 1) - 108);
                p += 2;
            }
            else
            {
                throw FontBytes.Corrupt("has an invalid byte in a CFF DICT");
            }
        }
        return dict;
    }

    public bool Has(int op) => _entries.ContainsKey(op);

    /// <summary>
    /// Returns operand <paramref name="index"/> of <paramref name="op"/> as an integer, or <paramref name="fallback"/>
    /// when the operator is absent.
    /// </summary>
    public int Int(int op, int index = 0, int fallback = 0)
    {
        if (!_entries.TryGetValue(op, out var operands)) return fallback;
        if (index >= operands.Length) throw FontBytes.Corrupt("has a CFF DICT operator without its operands");
        double value = operands[index];
        if (value < int.MinValue || value > int.MaxValue || double.IsNaN(value)) throw FontBytes.Corrupt("has an out-of-range CFF DICT value");
        return (int)value;
    }

    public double Number(int op, int index = 0, double fallback = 0) =>
        _entries.TryGetValue(op, out var operands) && index < operands.Length ? operands[index] : fallback;

    #endregion

    #region Private Methods

    private static PdfFontException Truncated() => FontBytes.Corrupt("has a truncated CFF DICT");

    // Reads a real number written as nibbles (CFF section 4): digits, a decimal point, exponents, a minus sign, and 0xF
    // to end it.
    private static int ReadReal(FontBytes data, int p, int end, out double value)
    {
        var text = new StringBuilder();
        while (true)
        {
            if (p >= end || text.Length > 64) throw FontBytes.Corrupt("has an invalid real number in a CFF DICT");
            int b = data.U8(p++);
            foreach (int nibble in new[] { b >> 4, b & 0xF })
            {
                switch (nibble)
                {
                    case <= 9: text.Append((char)('0' + nibble)); break;
                    case 0xA: text.Append('.'); break;
                    case 0xB: text.Append('E'); break;
                    case 0xC: text.Append("E-"); break;
                    case 0xE: text.Append('-'); break;
                    case 0xF:
                        value = double.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
                        return p;
                    default: throw FontBytes.Corrupt("has an invalid real number in a CFF DICT");
                }
            }
        }
    }

    #endregion

    #endregion
}
