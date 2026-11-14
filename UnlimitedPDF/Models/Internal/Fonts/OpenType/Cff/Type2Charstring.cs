namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// Walks a Type 2 charstring with its subroutines and returns it flattened: every <c>callsubr</c> and
/// <c>callgsubr</c> replaced by the subroutine's own operators, so the result needs no subroutines. Load uses this to
/// check every glyph (A06: subroutine nesting at most 10, at most 48 operands, a size budget); subsetting uses the
/// result to write subroutine-free glyphs.
/// </summary>
internal static class Type2Charstring
{
    #region Fields and Constants

    // Type 2 limits (Adobe Technical Note 5177): subroutine nesting and the argument stack.
    public const int MaxSubroutineDepth = 10;
    public const int MaxOperands = 48;

    // A flattened glyph larger than this is treated as hostile (subroutines that call each other repeatedly).
    public const int MaxFlattenedLength = 65536;

    private const int CallSubr = 10;
    private const int Return = 11;
    private const int Escape = 12;
    private const int EndChar = 14;
    private const int HintMask = 19;
    private const int CntrMask = 20;
    private const int CallGSubr = 29;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the subroutine bias for a subroutine INDEX with <paramref name="count"/> entries.
    /// </summary>
    public static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    /// <summary>
    /// Flattens one glyph's charstring.
    /// </summary>
    /// <param name="charstring">The glyph's charstring bytes.</param>
    /// <param name="localSubrs">The glyph's local subroutines, or null.</param>
    /// <param name="globalSubrs">The global subroutines.</param>
    /// <returns>The charstring without subroutine calls.</returns>
    public static byte[] Flatten(byte[] charstring, CffIndex? localSubrs, CffIndex globalSubrs)
    {
        var state = new State(localSubrs, globalSubrs);
        bool ended = Run(charstring, state, depth: 0);
        if (!ended) throw FontBytes.Corrupt("has a CFF glyph that does not end with endchar");
        return state.Output.ToArray();
    }

    #endregion

    #region Private Methods

    // Returns true when endchar was reached; false when the charstring (a subroutine) returned.
    private static bool Run(byte[] code, State state, int depth)
    {
        int p = 0;
        while (p < code.Length)
        {
            int b0 = code[p];
            if (b0 >= 32 || b0 == 28)
            {
                int length = b0 == 28 ? 3 : b0 <= 246 ? 1 : b0 <= 254 ? 2 : 5;
                if (p + length > code.Length) throw FontBytes.Corrupt("has a truncated CFF glyph");
                if (state.Operands.Count >= MaxOperands) throw FontBytes.Corrupt($"has a CFF glyph with more than {MaxOperands} operands");
                state.Operands.Add((Value(code, p, b0), code.AsSpan(p, length).ToArray()));
                p += length;
                continue;
            }

            p++;
            switch (b0)
            {
                case CallSubr:
                case CallGSubr:
                {
                    if (depth >= MaxSubroutineDepth) throw FontBytes.Corrupt($"nests CFF subroutines deeper than {MaxSubroutineDepth} levels");
                    var subrs = b0 == CallSubr ? state.LocalSubrs : state.GlobalSubrs;
                    if (subrs is null || state.Operands.Count == 0) throw FontBytes.Corrupt("calls a CFF subroutine that does not exist");
                    var (number, _) = state.Operands[^1];
                    state.Operands.RemoveAt(state.Operands.Count - 1);
                    int index = (int)number + Bias(subrs.Count);
                    if (number != Math.Floor(number) || index < 0 || index >= subrs.Count) throw FontBytes.Corrupt("calls a CFF subroutine that does not exist");
                    if (Run(subrs.Bytes(index), state, depth + 1)) return true;
                    break;
                }
                case Return:
                    if (depth == 0) throw FontBytes.Corrupt("returns from a CFF glyph outside a subroutine");
                    return false;
                case EndChar:
                    Emit(state, b0);
                    return true;
                case HintMask:
                case CntrMask:
                {
                    // Operands before the first mask are an implicit vstem; an odd count means a leading width.
                    CountStems(state);
                    Emit(state, b0);
                    int maskBytes = (state.Stems + 7) / 8;
                    if (p + maskBytes > code.Length) throw FontBytes.Corrupt("has a truncated CFF hint mask");
                    state.Write(code.AsSpan(p, maskBytes));
                    p += maskBytes;
                    break;
                }
                case Escape:
                {
                    if (p >= code.Length) throw FontBytes.Corrupt("has a truncated CFF glyph");
                    int b1 = code[p++];
                    Emit(state, b0, b1);
                    break;
                }
                case 1: // hstem
                case 3: // vstem
                case 18: // hstemhm
                case 23: // vstemhm
                    CountStems(state);
                    Emit(state, b0);
                    break;
                default:
                    if (b0 is 0 or 2 or 9 or 13 or 15 or 16 or 17) throw FontBytes.Corrupt($"uses the reserved CFF operator {b0}");
                    Emit(state, b0);
                    break;
            }
            if (state.Output.Length > MaxFlattenedLength) throw FontBytes.Corrupt("has a CFF glyph that expands too far through subroutines");
        }
        return false;
    }

    // Adds the stems the pending operands describe: pairs, after an optional leading width.
    private static void CountStems(State state)
    {
        int count = state.Operands.Count;
        if (!state.WidthSeen && count % 2 == 1) count--;
        state.WidthSeen = true;
        state.Stems += count / 2;
    }

    // Writes the pending operands, then the operator, and clears the operands.
    private static void Emit(State state, int op, int? escaped = null)
    {
        foreach (var (_, bytes) in state.Operands) state.Write(bytes);
        state.Operands.Clear();
        state.Output.WriteByte((byte)op);
        if (escaped is int b1) state.Output.WriteByte((byte)b1);
        if (op is not (1 or 3 or 18 or 23 or HintMask or CntrMask)) state.WidthSeen = true;
    }

    private static double Value(byte[] code, int p, int b0) => b0 switch
    {
        28 => (short)((code[p + 1] << 8) | code[p + 2]),
        <= 246 => b0 - 139,
        <= 250 => (b0 - 247) * 256 + code[p + 1] + 108,
        <= 254 => -(b0 - 251) * 256 - code[p + 1] - 108,
        _ => ((code[p + 1] << 24) | (code[p + 2] << 16) | (code[p + 3] << 8) | code[p + 4]) / 65536.0 // 16.16 fixed
    };

    #endregion

    #endregion

    #region Nested Types

    private sealed class State
    {
        public State(CffIndex? localSubrs, CffIndex globalSubrs)
        {
            LocalSubrs = localSubrs;
            GlobalSubrs = globalSubrs;
        }

        public CffIndex GlobalSubrs { get; }

        public CffIndex? LocalSubrs { get; }

        public List<(double Value, byte[] Bytes)> Operands { get; } = new();

        public MemoryStream Output { get; } = new();

        public int Stems { get; set; }

        public bool WidthSeen { get; set; }

        public void Write(ReadOnlySpan<byte> bytes) => Output.Write(bytes);
    }

    #endregion
}
