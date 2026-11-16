using UnlimitedPDF.Models.Internal.Fonts.OpenType;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Turns glyph outlines into SVG path data in font units, y up: TrueType quadratic contours (with composite glyphs)
/// and Type 2 charstrings (cubic).
/// </summary>
internal static class GlyphOutlines
{
    #region Fields and Constants

    private const int MaxComponentDepth = 16;
    private const int MaxStack = 48;

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the path of a TrueType glyph, following composite components.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="glyph">The glyph ID.</param>
    /// <returns>The SVG path data; empty for a glyph without outlines.</returns>
    public static string TrueType(OpenTypeFont font, int glyph)
    {
        var path = new StringBuilder();
        AppendTrueType(font, glyph, RenderMatrix.Identity, path, 0);
        return path.ToString();
    }

    /// <summary>
    /// Returns the path of a flattened Type 2 charstring (no subroutine calls).
    /// </summary>
    /// <param name="charstring">The charstring.</param>
    /// <returns>The SVG path data; empty for a glyph without outlines.</returns>
    public static string Type2(byte[] charstring)
    {
        var path = new StringBuilder();
        var stack = new List<double>();
        double x = 0, y = 0;
        int stems = 0;
        bool widthDone = false, open = false;

        void MoveTo(double dx, double dy)
        {
            if (open) path.Append('Z');
            x += dx; y += dy;
            path.Append('M').Append(SvgText.Number(x)).Append(' ').Append(SvgText.Number(y));
            open = true;
        }

        void LineTo(double dx, double dy)
        {
            x += dx; y += dy;
            path.Append('L').Append(SvgText.Number(x)).Append(' ').Append(SvgText.Number(y));
        }

        void CurveTo(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
        {
            double x1 = x + dx1, y1 = y + dy1, x2 = x1 + dx2, y2 = y1 + dy2;
            x = x2 + dx3; y = y2 + dy3;
            path.Append('C').Append(SvgText.Number(x1)).Append(' ').Append(SvgText.Number(y1)).Append(' ')
                .Append(SvgText.Number(x2)).Append(' ').Append(SvgText.Number(y2)).Append(' ')
                .Append(SvgText.Number(x)).Append(' ').Append(SvgText.Number(y));
        }

        // The first stack-clearing operator may carry the advance width as an extra leading operand.
        void DropWidth(bool hasExtra)
        {
            if (!widthDone && hasExtra && stack.Count > 0) stack.RemoveAt(0);
            widthDone = true;
        }

        for (int i = 0; i < charstring.Length;)
        {
            int b0 = charstring[i];
            if (b0 >= 32 || b0 == 28)
            {
                if (stack.Count >= MaxStack) throw new PageRenderException("A font glyph has too many operands.");
                double value;
                if (b0 == 28) { Need(charstring, i, 3); value = (short)((charstring[i + 1] << 8) | charstring[i + 2]); i += 3; }
                else if (b0 <= 246) { value = b0 - 139; i++; }
                else if (b0 <= 250) { Need(charstring, i, 2); value = (b0 - 247) * 256 + charstring[i + 1] + 108; i += 2; }
                else if (b0 <= 254) { Need(charstring, i, 2); value = -(b0 - 251) * 256 - charstring[i + 1] - 108; i += 2; }
                else { Need(charstring, i, 5); value = ((charstring[i + 1] << 24) | (charstring[i + 2] << 16) | (charstring[i + 3] << 8) | charstring[i + 4]) / 65536.0; i += 5; }
                stack.Add(value);
                continue;
            }

            int op = b0;
            i++;
            if (op == 12) { Need(charstring, i - 1, 2); op = 1200 + charstring[i++]; }
            var s = stack;
            switch (op)
            {
                case 1: case 3: case 18: case 23: // hstem, vstem, hstemhm, vstemhm
                    DropWidth(s.Count % 2 == 1);
                    stems += s.Count / 2;
                    break;
                case 19: case 20: // hintmask, cntrmask: implicit vstems, then the mask bytes
                    DropWidth(s.Count % 2 == 1);
                    stems += s.Count / 2;
                    i += (stems + 7) / 8;
                    break;
                case 21: // rmoveto
                    DropWidth(s.Count > 2);
                    Require(s, 2);
                    MoveTo(s[0], s[1]);
                    break;
                case 22: // hmoveto
                    DropWidth(s.Count > 1);
                    Require(s, 1);
                    MoveTo(s[0], 0);
                    break;
                case 4: // vmoveto
                    DropWidth(s.Count > 1);
                    Require(s, 1);
                    MoveTo(0, s[0]);
                    break;
                case 5: // rlineto
                    RequireOpen(open);
                    for (int k = 0; k + 1 < s.Count; k += 2) LineTo(s[k], s[k + 1]);
                    break;
                case 6: case 7: // hlineto, vlineto: alternating directions
                {
                    RequireOpen(open);
                    bool horizontal = op == 6;
                    foreach (double d in s)
                    {
                        if (horizontal) LineTo(d, 0); else LineTo(0, d);
                        horizontal = !horizontal;
                    }
                    break;
                }
                case 8: // rrcurveto
                    RequireOpen(open);
                    for (int k = 0; k + 5 < s.Count; k += 6) CurveTo(s[k], s[k + 1], s[k + 2], s[k + 3], s[k + 4], s[k + 5]);
                    break;
                case 24: // rcurveline
                {
                    RequireOpen(open);
                    int k = 0;
                    for (; k + 7 < s.Count; k += 6) CurveTo(s[k], s[k + 1], s[k + 2], s[k + 3], s[k + 4], s[k + 5]);
                    Require(s, k + 2);
                    LineTo(s[k], s[k + 1]);
                    break;
                }
                case 25: // rlinecurve
                {
                    RequireOpen(open);
                    int k = 0;
                    for (; k + 7 < s.Count; k += 2) LineTo(s[k], s[k + 1]);
                    Require(s, k + 6);
                    CurveTo(s[k], s[k + 1], s[k + 2], s[k + 3], s[k + 4], s[k + 5]);
                    break;
                }
                case 26: // vvcurveto
                {
                    RequireOpen(open);
                    int k = 0;
                    double dx1 = 0;
                    if (s.Count % 4 == 1) dx1 = s[k++];
                    for (; k + 3 < s.Count; k += 4) { CurveTo(dx1, s[k], s[k + 1], s[k + 2], 0, s[k + 3]); dx1 = 0; }
                    break;
                }
                case 27: // hhcurveto
                {
                    RequireOpen(open);
                    int k = 0;
                    double dy1 = 0;
                    if (s.Count % 4 == 1) dy1 = s[k++];
                    for (; k + 3 < s.Count; k += 4) { CurveTo(s[k], dy1, s[k + 1], s[k + 2], s[k + 3], 0); dy1 = 0; }
                    break;
                }
                case 30: case 31: // vhcurveto, hvcurveto: alternating start directions
                {
                    RequireOpen(open);
                    bool horizontal = op == 31;
                    int k = 0;
                    while (k + 3 < s.Count)
                    {
                        double last = s.Count - k == 5 ? s[k + 4] : 0;
                        if (horizontal) CurveTo(s[k], 0, s[k + 1], s[k + 2], last, s[k + 3]);
                        else CurveTo(0, s[k], s[k + 1], s[k + 2], s[k + 3], last);
                        k += 4;
                        horizontal = !horizontal;
                    }
                    break;
                }
                case 1235: // flex
                    RequireOpen(open);
                    Require(s, 13);
                    CurveTo(s[0], s[1], s[2], s[3], s[4], s[5]);
                    CurveTo(s[6], s[7], s[8], s[9], s[10], s[11]);
                    break;
                case 1234: // hflex
                    RequireOpen(open);
                    Require(s, 7);
                    CurveTo(s[0], 0, s[1], s[2], s[3], 0);
                    CurveTo(s[4], 0, s[5], -s[2], s[6], 0);
                    break;
                case 1236: // hflex1
                    RequireOpen(open);
                    Require(s, 9);
                    CurveTo(s[0], s[1], s[2], s[3], s[4], 0);
                    CurveTo(s[5], 0, s[6], s[7], s[8], -(s[1] + s[3] + s[7]));
                    break;
                case 1237: // flex1
                {
                    RequireOpen(open);
                    Require(s, 11);
                    double dx = s[0] + s[2] + s[4] + s[6] + s[8], dy = s[1] + s[3] + s[5] + s[7] + s[9];
                    double startX = x, startY = y;
                    CurveTo(s[0], s[1], s[2], s[3], s[4], s[5]);
                    if (Math.Abs(dx) > Math.Abs(dy)) CurveTo(s[6], s[7], s[8], s[9], s[10], startY - (y + s[7] + s[9]));
                    else CurveTo(s[6], s[7], s[8], s[9], startX - (x + s[6] + s[8]), s[10]);
                    break;
                }
                case 14: // endchar
                    if (s.Count is 4 or 5) throw new PageRenderException("A font glyph uses accented-character composition, which this preview doesn't draw.");
                    DropWidth(s.Count == 1);
                    if (open) path.Append('Z');
                    return path.ToString();
                default:
                    throw new PageRenderException("A font glyph uses a charstring operator this preview doesn't draw.");
            }
            s.Clear();
        }

        if (open) path.Append('Z');
        return path.ToString();
    }

    #endregion

    #region Private Methods

    private static void AppendTrueType(OpenTypeFont font, int glyph, RenderMatrix transform, StringBuilder path, int depth)
    {
        if (depth > MaxComponentDepth) throw new PageRenderException("A font glyph nests components too deeply.");
        if (glyph < 0 || glyph >= font.GlyphCount) throw new PageRenderException("A font glyph refers to a glyph the font doesn't have.");

        var decoded = TrueTypeGlyph.Decode(font, glyph);
        if (decoded.Components.Length > 0)
        {
            foreach (var component in decoded.Components)
            {
                if (!component.HasOffsets) throw new PageRenderException("A font glyph places a component by points, which this preview doesn't draw.");
                var t = component.Transform;
                double a = 1, b = 0, c = 0, d = 1;
                if (t.Length == 2) a = d = F2Dot14(t, 0);
                else if (t.Length == 4) { a = F2Dot14(t, 0); d = F2Dot14(t, 2); }
                else if (t.Length == 8) { a = F2Dot14(t, 0); b = F2Dot14(t, 2); c = F2Dot14(t, 4); d = F2Dot14(t, 6); }
                var local = new RenderMatrix(a, b, c, d, component.Argument1, component.Argument2);
                AppendTrueType(font, component.Glyph, local.Then(transform), path, depth + 1);
            }
            return;
        }

        int start = 0;
        foreach (int end in decoded.ContourEnds)
        {
            AppendContour(decoded, start, end, transform, path);
            start = end + 1;
        }
    }

    // One closed quadratic contour. A contour may start off-curve; then it starts at the midpoint of its first two
    // points, or at its last point when that one is on-curve.
    private static void AppendContour(TrueTypeGlyph glyph, int start, int end, RenderMatrix t, StringBuilder path)
    {
        int count = end - start + 1;
        if (count <= 0) return;
        (double X, double Y) Point(int i) => Map(t, glyph.X[start + (i % count)], glyph.Y[start + (i % count)]);
        bool On(int i) => glyph.OnCurvePoints[start + (i % count)];

        (double X, double Y) first;
        int offset;
        if (On(0)) { first = Point(0); offset = 1; }
        else if (On(count - 1)) { first = Point(count - 1); offset = 0; count--; }
        else { var p0 = Point(0); var p1 = Point(1); first = ((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2); offset = 1; }

        path.Append('M').Append(SvgText.Number(first.X)).Append(' ').Append(SvgText.Number(first.Y));
        (double X, double Y)? control = null;
        for (int k = offset; k <= count; k++)
        {
            var p = k == count ? first : Point(k);
            bool on = k == count || On(k);
            if (on)
            {
                if (control is { } q) AppendQuad(path, q, p);
                else if (k < count) path.Append('L').Append(SvgText.Number(p.X)).Append(' ').Append(SvgText.Number(p.Y)); // Z closes back to the start
                control = null;
            }
            else if (control is { } q)
            {
                var mid = ((q.X + p.X) / 2, (q.Y + p.Y) / 2);
                AppendQuad(path, q, mid);
                control = p;
            }
            else
            {
                control = p;
            }
        }
        if (control is { } last) AppendQuad(path, last, first);
        path.Append('Z');
    }

    private static void AppendQuad(StringBuilder path, (double X, double Y) control, (double X, double Y) end) =>
        path.Append('Q').Append(SvgText.Number(control.X)).Append(' ').Append(SvgText.Number(control.Y)).Append(' ')
            .Append(SvgText.Number(end.X)).Append(' ').Append(SvgText.Number(end.Y));

    private static double F2Dot14(byte[] bytes, int offset) => (short)((bytes[offset] << 8) | bytes[offset + 1]) / 16384.0;

    private static (double X, double Y) Map(RenderMatrix m, double x, double y) => (m.A * x + m.C * y + m.E, m.B * x + m.D * y + m.F);

    private static void Need(byte[] data, int at, int length)
    {
        if (at + length > data.Length) throw new PageRenderException("A font glyph ends inside a number.");
    }

    private static void Require(List<double> stack, int count)
    {
        if (stack.Count < count) throw new PageRenderException("A font glyph has too few operands.");
    }

    private static void RequireOpen(bool open)
    {
        if (!open) throw new PageRenderException("A font glyph draws before it moves.");
    }

    #endregion

    #endregion
}
