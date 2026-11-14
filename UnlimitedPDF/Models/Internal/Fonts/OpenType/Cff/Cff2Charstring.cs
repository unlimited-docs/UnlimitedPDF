using UnlimitedPDF.Models.Internal.Fonts.OpenType.Variation;

namespace UnlimitedPDF.Models.Internal.Fonts.OpenType.Cff;

/// <summary>
/// Turns a CFF2 charstring into a CFF version 1 charstring at fixed variation coordinates (D-013): every
/// <c>blend</c> is evaluated, <c>vsindex</c> dropped, subroutines expanded (at most 10 levels, A06), <c>endchar</c>
/// appended, and long <c>rlineto</c> and <c>rrcurveto</c> runs split to stay within CFF version 1's 48-operand
/// limit. CFF2 allows 513 operands.
/// </summary>
internal static class Cff2Charstring
{
    #region Fields and Constants

    public const int MaxOperands = 513;

    private const int RLineTo = 5;
    private const int RRCurveTo = 8;
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

    public static byte[] Instance(byte[] charstring, CffIndex? localSubrs, CffIndex globalSubrs, ItemVariationStore? store, int vsIndex, double[] coordinates)
    {
        var state = new State(localSubrs, globalSubrs, store, vsIndex, coordinates);
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
                    Emit(state, b0);
                    break;
                case RLineTo:
                    EmitSplit(state, b0, 2);
                    break;
                case RRCurveTo:
                    EmitSplit(state, b0, 6);
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
        var scalars = state.Store.Scalars(state.VsIndex, state.Coordinates);
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
        public State(CffIndex? localSubrs, CffIndex globalSubrs, ItemVariationStore? store, int vsIndex, double[] coordinates)
        {
            LocalSubrs = localSubrs;
            GlobalSubrs = globalSubrs;
            Store = store;
            VsIndex = vsIndex;
            Coordinates = coordinates;
        }

        public double[] Coordinates { get; }

        public CffIndex GlobalSubrs { get; }

        public CffIndex? LocalSubrs { get; }

        public MemoryStream Output { get; } = new();

        public List<double> Stack { get; } = new();

        public int Stems { get; set; }

        public ItemVariationStore? Store { get; }

        public int VsIndex { get; set; }
    }

    #endregion
}
