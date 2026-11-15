using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// Turns a CFF2 charstring into a CFF version 1 charstring at fixed variation coordinates (D-013): every
/// <c>blend</c> is evaluated, <c>vsindex</c> dropped, subroutines expanded (at most 10 levels, A06), <c>endchar</c>
/// appended, and runs longer than CFF version 1's 48-operand limit (CFF2 allows 513) split into valid groups: line and
/// curve runs keep their meaning group by group, and stem hints start each later group from an absolute edge.
/// </summary>
internal static class Cff2Charstring
{
    #region Fields and Constants

    public const int MaxOperands = 513;

    private const int RLineTo = 5;
    private const int HLineTo = 6;
    private const int VLineTo = 7;
    private const int RRCurveTo = 8;
    private const int VStemHm = 23;
    private const int RCurveLine = 24;
    private const int RLineCurve = 25;
    private const int CallSubr = 10;
    private const int Escape = 12;
    private const int EndChar = 14;
    private const int VsIndex = 15;
    private const int Blend = 16;
    private const int HintMask = 19;
    private const int CntrMask = 20;
    private const int CallGSubr = 29;

    #endregion

    #region Methods

    #region Public Methods

    public static byte[] Instance(byte[] charstring, CffIndex? localSubrs, CffIndex globalSubrs, ItemVariationStore? store, int vsIndex, double[] coordinates, CharstringWork? work = null)
    {
        var state = new State(localSubrs, globalSubrs, store, vsIndex, coordinates, work ?? new CharstringWork(1));
        Run(charstring, state, depth: 0);
        if (state.Stack.Count > 0) throw FontBytes.Corrupt("has a CFF2 glyph that ends with unused operands");
        state.Output.WriteByte(EndChar);
        return state.Output.ToArray();
    }

    #endregion

    #region Private Methods

    private static void Run(byte[] code, State state, int depth)
    {
        int p = 0;
        while (p < code.Length)
        {
            // The same step budget as CFF (A06): calls that draw nothing never grow the output.
            if (++state.Steps > Type2Charstring.MaxSteps) throw FontBytes.Corrupt("has a CFF2 glyph that runs too long through subroutines");
            state.Work.Step();
            int b0 = code[p];
            if (b0 >= 32 || b0 == 28)
            {
                int length = b0 == 28 ? 3 : b0 <= 246 ? 1 : b0 <= 254 ? 2 : 5;
                if (p + length > code.Length) throw FontBytes.Corrupt("has a truncated CFF2 glyph");
                Push(state, Value(code, p, b0));
                p += length;
                continue;
            }

            p++;
            switch (b0)
            {
                case CallSubr:
                case CallGSubr:
                {
                    if (depth >= Type2Charstring.MaxSubroutineDepth) throw FontBytes.Corrupt($"nests CFF subroutines deeper than {Type2Charstring.MaxSubroutineDepth} levels");
                    var subrs = b0 == CallSubr ? state.LocalSubrs : state.GlobalSubrs;
                    if (subrs is null || state.Stack.Count == 0) throw FontBytes.Corrupt("calls a CFF subroutine that does not exist");
                    double number = Pop(state);
                    int index = (int)number + Type2Charstring.Bias(subrs.Count);
                    if (number != Math.Floor(number) || index < 0 || index >= subrs.Count) throw FontBytes.Corrupt("calls a CFF subroutine that does not exist");
                    Run(subrs.Bytes(index), state, depth + 1);
                    break;
                }
                case VsIndex:
                    if (state.Stack.Count == 0) throw FontBytes.Corrupt("has a CFF2 vsindex without its operand");
                    state.VsIndex = (int)Pop(state);
                    break;
                case Blend:
                    DoBlend(state);
                    break;
                case HintMask:
                case CntrMask:
                {
                    state.Stems += state.Stack.Count / 2; // operands before the first mask are an implicit vstem
                    if (state.Stack.Count > Type2Charstring.MaxOperands) EmitStems(state, VStemHm); // written out explicitly
                    Emit(state, b0);
                    int maskBytes = (state.Stems + 7) / 8;
                    if (p + maskBytes > code.Length) throw FontBytes.Corrupt("has a truncated CFF2 hint mask");
                    state.Output.Write(code, p, maskBytes);
                    p += maskBytes;
                    break;
                }
                case 1: // hstem
                case 3: // vstem
                case 18: // hstemhm
                case 23: // vstemhm
                    state.Stems += state.Stack.Count / 2;
                    EmitStems(state, b0);
                    break;
                case RLineTo:
                    EmitSplit(state, b0, 2);
                    break;
                case HLineTo:
                case VLineTo:
                    EmitSplitAlternatingLines(state, b0);
                    break;
                case RCurveLine:
                    EmitSplitWithTail(state, b0, RRCurveTo, group: 6);
                    break;
                case RLineCurve:
                    EmitSplitWithTail(state, b0, RLineTo, group: 2);
                    break;
                case RRCurveTo:
                    EmitSplit(state, b0, 6);
                    break;
                case 26: // vvcurveto
                case 27: // hhcurveto
                    EmitSplitLeading(state, b0);
                    break;
                case 30: // vhcurveto
                case 31: // hvcurveto
                    EmitSplitAlternating(state, b0);
                    break;
                case Escape:
                {
                    if (p >= code.Length) throw FontBytes.Corrupt("has a truncated CFF2 glyph");
                    Emit(state, b0, code[p++]);
                    break;
                }
                default:
                    if (b0 is 0 or 2 or 9 or 11 or 13 or 14 or 17) throw FontBytes.Corrupt($"uses the operator {b0}, which CFF2 does not allow");
                    Emit(state, b0);
                    break;
            }
            if (state.Output.Length > Type2Charstring.MaxFlattenedLength) throw FontBytes.Corrupt("has a CFF glyph that expands too far through subroutines");
        }
    }

    // blend: n default values, then n x k deltas (k = regions of the current vsindex), then n; leaves n values.
    private static void DoBlend(State state)
    {
        if (state.Store is null || state.Stack.Count == 0) throw FontBytes.Corrupt("blends without a CFF2 variation store");
        double nValue = Pop(state);
        int n = (int)nValue;
        int k = state.Store.RegionCount(state.VsIndex);
        if (nValue != n || n < 0 || state.Stack.Count < (long)n * (k + 1)) throw FontBytes.Corrupt("has a CFF2 blend without enough operands");
        var scalars = state.Work.Scalars(state.Store, state.VsIndex, state.Coordinates);
        int baseIndex = state.Stack.Count - n * (k + 1);
        for (int i = 0; i < n; i++)
        {
            double value = state.Stack[baseIndex + i];
            for (int j = 0; j < k; j++) value += scalars[j] * state.Stack[baseIndex + n + i * k + j];
            state.Stack[baseIndex + i] = value;
        }
        state.Stack.RemoveRange(baseIndex + n, n * k);
    }

    private static void Emit(State state, int op, int? escaped = null)
    {
        if (state.Stack.Count > Type2Charstring.MaxOperands)
            throw new PdfFontException(PdfFontErrorReason.UnsupportedFormat, "The font's CFF2 outlines use more operands in one operator than CFF allows.");
        foreach (double value in state.Stack) WriteNumber(state.Output, value);
        state.Stack.Clear();
        state.Output.WriteByte((byte)op);
        if (escaped is int b1) state.Output.WriteByte((byte)b1);
    }

    // Splits a run of argument groups (pairs for rlineto, sextuples for rrcurveto) into calls of at most 48 operands.
    private static void EmitSplit(State state, int op, int group)
    {
        if (state.Stack.Count % group != 0) throw FontBytes.Corrupt("has a CFF2 path operator with the wrong number of operands");
        int chunk = Type2Charstring.MaxOperands / group * group;
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        for (int i = 0; i < values.Length; i += chunk)
        {
            for (int j = i; j < Math.Min(values.Length, i + chunk); j++) WriteNumber(state.Output, values[j]);
            state.Output.WriteByte((byte)op);
        }
    }

    // Splits an hhcurveto or vvcurveto run (an optional leading operand, then curves of 4) into calls of at most 48
    // operands; the leading operand stays with the first curve.
    private static void EmitSplitLeading(State state, int op)
    {
        int count = state.Stack.Count;
        if (count < 4 || count % 4 > 1) throw FontBytes.Corrupt("has a CFF2 curve operator with the wrong number of operands");
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        int i = 0;
        while (i < values.Length)
        {
            int take = Math.Min(values.Length - i, (i == 0 && count % 4 == 1) ? 1 + 44 : 48);
            for (int j = i; j < i + take; j++) WriteNumber(state.Output, values[j]);
            state.Output.WriteByte((byte)op);
            i += take;
        }
    }

    // Splits an hvcurveto or vhcurveto run (curves of 4 that alternate between starting horizontally and vertically,
    // with an optional final operand) into calls of at most 48 operands. Each split comes after an even number of
    // curves, so every call starts in the same direction, and the final operand stays with the last curve.
    private static void EmitSplitAlternating(State state, int op)
    {
        int count = state.Stack.Count;
        if (count < 4 || count % 4 > 1) throw FontBytes.Corrupt("has a CFF2 curve operator with the wrong number of operands");
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        int i = 0;
        while (values.Length - i > Type2Charstring.MaxOperands)
        {
            for (int j = i; j < i + 40; j++) WriteNumber(state.Output, values[j]);
            state.Output.WriteByte((byte)op);
            i += 40;
        }
        for (int j = i; j < values.Length; j++) WriteNumber(state.Output, values[j]);
        state.Output.WriteByte((byte)op);
    }

    // Splits an hlineto or vlineto run (single operands that alternate between horizontal and vertical lines) after an
    // even number of lines, so every call starts in the same direction.
    private static void EmitSplitAlternatingLines(State state, int op)
    {
        if (state.Stack.Count == 0) throw FontBytes.Corrupt("has a CFF2 line operator without operands");
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        for (int i = 0; i < values.Length; i += Type2Charstring.MaxOperands)
        {
            for (int j = i; j < Math.Min(values.Length, i + Type2Charstring.MaxOperands); j++) WriteNumber(state.Output, values[j]);
            state.Output.WriteByte((byte)op);
        }
    }

    // Splits an rcurveline run (curves of 6, then one line of 2) or an rlinecurve run (lines of 2, then one curve of 6):
    // the leading groups go out as rrcurveto or rlineto calls of 42 operands until the rest, which keeps at least one
    // leading group and the tail, fits in one call.
    private static void EmitSplitWithTail(State state, int op, int leadingOp, int group)
    {
        int tail = 8 - group; // the line after curves, or the curve after lines
        int count = state.Stack.Count;
        if (count < group + tail || (count - tail) % group != 0) throw FontBytes.Corrupt("has a CFF2 path operator with the wrong number of operands");
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        int i = 0;
        while (values.Length - i > Type2Charstring.MaxOperands)
        {
            for (int j = i; j < i + 42; j++) WriteNumber(state.Output, values[j]);
            state.Output.WriteByte((byte)leadingOp);
            i += 42;
        }
        for (int j = i; j < values.Length; j++) WriteNumber(state.Output, values[j]);
        state.Output.WriteByte((byte)op);
    }

    // Writes stem hints (edge and width pairs, each edge relative to the previous one) in calls of at most 24 stems.
    // Each stem operator measures its first edge from zero, so a later call starts from the absolute edge.
    private static void EmitStems(State state, int op)
    {
        if (state.Stack.Count <= Type2Charstring.MaxOperands)
        {
            Emit(state, op);
            return;
        }
        if (state.Stack.Count % 2 != 0) throw FontBytes.Corrupt("has CFF2 stem hints with an odd number of operands");
        var values = state.Stack.ToArray();
        state.Stack.Clear();
        double edge = 0;
        for (int i = 0; i < values.Length; i += Type2Charstring.MaxOperands)
        {
            int end = Math.Min(values.Length, i + Type2Charstring.MaxOperands);
            for (int j = i; j < end; j++)
            {
                WriteNumber(state.Output, j == i ? edge + values[j] : values[j]);
                edge += values[j];
            }
            state.Output.WriteByte((byte)op);
        }
    }

    private static void Push(State state, double value)
    {
        if (state.Stack.Count >= MaxOperands) throw FontBytes.Corrupt($"has a CFF2 glyph with more than {MaxOperands} operands");
        state.Stack.Add(value);
    }

    private static double Pop(State state)
    {
        double value = state.Stack[^1];
        state.Stack.RemoveAt(state.Stack.Count - 1);
        return value;
    }

    private static double Value(byte[] code, int p, int b0) => b0 switch
    {
        28 => (short)((code[p + 1] << 8) | code[p + 2]),
        <= 246 => b0 - 139,
        <= 250 => (b0 - 247) * 256 + code[p + 1] + 108,
        <= 254 => -(b0 - 251) * 256 - code[p + 1] - 108,
        _ => ((code[p + 1] << 24) | (code[p + 2] << 16) | (code[p + 3] << 8) | code[p + 4]) / 65536.0
    };

    // Integers in the 16-bit range use the compact forms; anything else is written as 16.16 fixed point.
    private static void WriteNumber(MemoryStream output, double value)
    {
        double rounded = Math.Round(value);
        if (Math.Abs(value - rounded) < 1e-6 && rounded >= short.MinValue && rounded <= short.MaxValue)
        {
            int v = (int)rounded;
            if (v >= -107 && v <= 107) output.WriteByte((byte)(v + 139));
            else if (v >= 108 && v <= 1131) { output.WriteByte((byte)((v - 108) / 256 + 247)); output.WriteByte((byte)((v - 108) % 256)); }
            else if (v >= -1131 && v <= -108) { output.WriteByte((byte)((-v - 108) / 256 + 251)); output.WriteByte((byte)((-v - 108) % 256)); }
            else { output.WriteByte(28); output.WriteByte((byte)(v >> 8)); output.WriteByte((byte)v); }
            return;
        }
        int fixedValue = (int)Math.Round(Math.Max(-32768, Math.Min(32767.99998, value)) * 65536);
        output.WriteByte(255);
        output.WriteByte((byte)(fixedValue >> 24));
        output.WriteByte((byte)(fixedValue >> 16));
        output.WriteByte((byte)(fixedValue >> 8));
        output.WriteByte((byte)fixedValue);
    }

    #endregion

    #endregion

    #region Nested Types

    private sealed class State
    {
        public State(CffIndex? localSubrs, CffIndex globalSubrs, ItemVariationStore? store, int vsIndex, double[] coordinates, CharstringWork work)
        {
            LocalSubrs = localSubrs;
            GlobalSubrs = globalSubrs;
            Store = store;
            VsIndex = vsIndex;
            Coordinates = coordinates;
            Work = work;
        }

        public double[] Coordinates { get; }

        public CffIndex GlobalSubrs { get; }

        public CffIndex? LocalSubrs { get; }

        public MemoryStream Output { get; } = new();

        public int Steps { get; set; }

        public List<double> Stack { get; } = new();

        public int Stems { get; set; }

        public ItemVariationStore? Store { get; }

        public int VsIndex { get; set; }

        public CharstringWork Work { get; }
    }

    #endregion
}
