using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    // Fonts already read, by font dictionary, so a font is read once per page; each gets a number for its glyph ids.
    private readonly Dictionary<PdfReadDictionary, RenderFont> _fonts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RenderFont, int> _fontNumbers = new(ReferenceEqualityComparer.Instance);

    // Glyph outlines already written to the definitions, by font number and glyph.
    private readonly HashSet<(int Font, int Glyph)> _glyphDefinitions = new();
    private readonly StringBuilder _line = new();

    // Open marked-content sequences; each holds its /ActualText, or null, and whether that text is in the page yet.
    private readonly Stack<MarkedSpan> _spans = new();
    private readonly List<string> _textLines = new();

    // The longest /ActualText read; real spans hold a syllable or a word.
    private const int MaxActualTextLength = 4096;

    private long _fontTextCharacters;
    private int _glyphComponents;
    private int _glyphs;
    private bool _inText;

    // Characters in finished text lines, counted with the drawing against the output bound.
    private long _textCharacters;

    // Every /ActualText character read on the page, never reset, so repeated spans can't add up unbounded work.
    private long _actualTextCharacters;
    private int _links;
    private RenderMatrix _lineMatrix = RenderMatrix.Identity;
    private RenderMatrix _textMatrix = RenderMatrix.Identity;

    #endregion

    #region Methods

    #region Private Methods

    // BDC or BMC: opens a marked-content sequence. A span with /ActualText gives the text its glyphs stand for.
    private void BeginMarkedContent(IReadOnlyList<PdfReadValue>? operands, PdfReadDictionary? resources)
    {
        string? actual = null;
        if (operands is { Count: >= 2 })
        {
            var properties = operands[^1] switch
            {
                PdfReadDictionary inline => inline,
                PdfReadName name => _objects.Resolve((_objects.Resolve(resources?["Properties"]) as PdfReadDictionary)?[name.Value]) as PdfReadDictionary,
                _ => null,
            };
            if (_objects.Resolve(properties?["ActualText"]) is PdfReadString text)
            {
                if (text.Bytes.Length > 2 * MaxActualTextLength + 2) throw new PageRenderException("The page has an /ActualText that is too long.");
                actual = XmlSafe(PdfTextString.Decode(text.Bytes));
                _actualTextCharacters += actual.Length;
                if (_actualTextCharacters > _limits.MaxOutputCharacters) throw new PageRenderException("The page has too much /ActualText.");
            }
        }

        if (_spans.Count >= _limits.MaxStateDepth) throw new PageRenderException("The page nests marked content too deeply.");
        _spans.Push(new MarkedSpan(actual));
        if (actual is not null)
        {
            _line.Append(actual);
            CheckOutput();
        }
    }

    private void EndMarkedContent()
    {
        if (_spans.Count > 0) _spans.Pop();
    }

    private RenderFont FontFor(PdfReadName name, PdfReadDictionary? resources)
    {
        var fonts = _objects.Resolve(resources?["Font"]) as PdfReadDictionary;
        if (_objects.Resolve(fonts?[name.Value]) is not PdfReadDictionary dictionary)
            throw new PageRenderException($"The page uses a font it doesn't have ({Shorten(name.Value)}).");
        if (_fonts.TryGetValue(dictionary, out var font)) return font;
        if (_fonts.Count >= _limits.MaxFonts) throw new PageRenderException("The page uses too many fonts.");

        font = (RenderFont?)StandardRenderFont.From(dictionary, _objects.Resolve)
            ?? EmbeddedRenderFont.From(dictionary, _objects.Resolve, Decode)
            ?? throw new PageRenderException("The page uses a kind of font this preview doesn't draw.");
        if (font is EmbeddedRenderFont embedded && (_fontTextCharacters += embedded.TextCharacters) > _limits.MaxFontTextCharacters)
            throw new PageRenderException("The page's fonts map too much text.");
        _fonts[dictionary] = font;
        _fontNumbers[font] = _fontNumbers.Count;
        return font;
    }

    // Runs a text or XObject operator; false when the operator is not one of them.
    private bool RunTextOrObject(string op, IReadOnlyList<PdfReadValue> operands, PdfReadDictionary? resources)
    {
        switch (op)
        {
            case "Do": // draw an XObject
                DrawXObject(operands, resources);
                return true;

            // Text objects
            case "BT": // begin text
                if (_inText) throw new PageRenderException("A text object on the page starts inside another.");
                _inText = true;
                _textMatrix = _lineMatrix = RenderMatrix.Identity;
                _line.Clear(); // as the text oracle does: only text inside BT ... ET makes a line
                return true;
            case "ET": // end text
                RequireText(op);
                _inText = false;
                _textLines.Add(_line.ToString());
                _textCharacters += _line.Length;
                _line.Clear();
                return true;

            // Text state
            case "Tf": // font and size
                if (operands.Count < 2 || operands[^2] is not PdfReadName fontName) throw new PageRenderException("A font is set wrongly.");
                _state.Font = FontFor(fontName, resources);
                _state.FontSize = Numbers(operands, 1)[0];
                return true;
            case "Tc": _state.CharacterSpacing = Numbers(operands, 1)[0]; return true; // character spacing
            case "Tw": _state.WordSpacing = Numbers(operands, 1)[0]; return true; // word spacing
            case "Tz": _state.HorizontalScale = Numbers(operands, 1)[0] / 100; return true; // horizontal scaling
            case "TL": _state.Leading = Numbers(operands, 1)[0]; return true; // leading
            case "Ts": _state.Rise = Numbers(operands, 1)[0]; return true; // rise
            case "Tr": // rendering mode
                _state.RenderMode = (int)Numbers(operands, 1)[0];
                if (_state.RenderMode is >= 4 and <= 7) throw new PageRenderException("The page clips with text, which this preview doesn't draw.");
                if (_state.RenderMode is < 0 or > 7) throw new PageRenderException("The page has a text rendering mode that doesn't exist.");
                return true;

            // Text position
            case "Td": { RequireText(op); var t = Numbers(operands, 2); MoveLine(t[0], t[1]); return true; } // next line, offset
            case "TD": { RequireText(op); var t = Numbers(operands, 2); _state.Leading = -t[1]; MoveLine(t[0], t[1]); return true; }
            case "T*": RequireText(op); MoveLine(0, -_state.Leading); return true; // next line
            case "Tm": // text matrix
            {
                RequireText(op);
                var m = Numbers(operands, 6);
                _textMatrix = _lineMatrix = new RenderMatrix(m[0], m[1], m[2], m[3], m[4], m[5]);
                return true;
            }

            // Text showing
            case "Tj": RequireText(op); Show(StringOperand(operands, 1), null); return true;
            case "'": RequireText(op); MoveLine(0, -_state.Leading); Show(StringOperand(operands, 1), null); return true;
            case "\"": // word and character spacing, next line, show
            {
                RequireText(op);
                var spacing = Numbers(operands.Take(operands.Count - 1).ToList(), 2);
                _state.WordSpacing = spacing[0];
                _state.CharacterSpacing = spacing[1];
                MoveLine(0, -_state.Leading);
                Show(StringOperand(operands, 1), null);
                return true;
            }
            case "TJ": // show with positions
                RequireText(op);
                if (operands.Count < 1 || operands[^1] is not PdfReadArray parts) throw new PageRenderException("A TJ array is not valid.");
                Show(null, parts);
                return true;

            default:
                return false;
        }
    }

    private void MoveLine(double x, double y)
    {
        _lineMatrix = RenderMatrix.Translation(x, y).Then(_lineMatrix);
        _textMatrix = _lineMatrix;
    }

    private void RequireText(string op)
    {
        if (!_inText) throw new PageRenderException($"The page uses the text operator {op} outside a text object.");
    }

    // Shows a string (Tj, ', ") or a TJ array, advancing the text matrix.
    private void Show(byte[]? text, PdfReadArray? parts)
    {
        var font = _state.Font ?? throw new PageRenderException("The page shows text before it sets a font.");
        double size = _state.FontSize;
        var positions = new StringBuilder();
        var characters = new StringBuilder();
        var placed = new List<(int Glyph, double X)>();
        double x = 0;
        double sign = size < 0 ? -1 : 1;
        // The outermost span with /ActualText gives the text (PDF: an enclosing span's text replaces what it holds).
        var actualSpan = _spans.LastOrDefault(span => span.ActualText is not null);
        bool layer = actualSpan is null;

        void ShowBytes(byte[] bytes)
        {
            // A string holds at least as many bytes as glyphs, so the glyph bound is checked before any glyph is made.
            if (_glyphs + (long)bytes.Length / (font is EmbeddedRenderFont ? 2 : 1) > _limits.MaxGlyphs)
                throw new PageRenderException("The page shows too many glyphs.");
            foreach (var glyph in font.Glyphs(bytes))
            {
                if (++_glyphs > _limits.MaxGlyphs) throw new PageRenderException("The page shows too many glyphs.");
                string glyphText = XmlSafe(glyph.Text);
                if (font is EmbeddedRenderFont)
                {
                    placed.Add((glyph.Code, x));
                    if (layer)
                    {
                        characters.Append(glyphText);
                        _line.Append(glyphText);
                    }
                }
                else if (glyphText.Length > 0)
                {
                    foreach (char c in glyphText)
                    {
                        if (char.IsLowSurrogate(c)) { characters.Append(c); continue; }
                        if (positions.Length > 0) positions.Append(' ');

                        // The text element's frame already carries the size's sign, so the position is unsigned.
                        positions.Append(SvgText.Number(x * sign));
                        characters.Append(c);
                    }
                    if (layer) _line.Append(glyphText);
                }
                x += glyph.Width / 1000 * size + _state.CharacterSpacing + (glyph.IsWordSpace ? _state.WordSpacing : 0);
                if (_body.Length + _defs.Length + positions.Length + characters.Length + _line.Length + _textCharacters > _limits.MaxOutputCharacters)
                    throw new PageRenderException("The page drawing is too large.");
            }
        }

        if (text is not null)
        {
            ShowBytes(text);
        }
        else
        {
            foreach (var part in parts!.Items)
            {
                if (part is PdfReadString s) ShowBytes(s.Bytes);
                else if (part is PdfReadNumber n) x -= (double.IsFinite(n.Value) ? n.Value : throw new PageRenderException("A TJ array holds a number that isn't finite.")) / 1000 * size;
                else throw new PageRenderException("A TJ array holds something other than strings and numbers.");
            }
        }

        if (characters.Length > 0 && font is StandardRenderFont standard) DrawStandardText(standard, positions, characters);
        if (font is EmbeddedRenderFont embedded)
        {
            DrawGlyphs(embedded, placed);

            // The glyphs are outlines, so the characters they stand for go in an invisible, selectable text run of
            // the same width; inside an /ActualText span, the span's text goes there once.
            string runText = characters.ToString();
            if (actualSpan is not null)
            {
                runText = actualSpan.Shown ? string.Empty : actualSpan.ActualText!;
                actualSpan.Shown = true;
            }
            if (runText.Length > 0) DrawTextLayer(runText, x);
        }
        _textMatrix = RenderMatrix.Translation(x * _state.HorizontalScale, 0).Then(_textMatrix);
        CheckOutput();
    }

    // Draws text in a standard font as SVG text, each character at the position the PDF's widths give it.
    private void DrawStandardText(StandardRenderFont font, StringBuilder positions, StringBuilder characters)
    {
        double size = _state.FontSize;
        double sign = size < 0 ? -1 : 1;

        // Text space to the page, then turned so SVG's y-down text stands upright.
        var frame = new RenderMatrix(_state.HorizontalScale * sign, 0, 0, sign, 0, _state.Rise).Then(_textMatrix).Then(_state.Ctm);
        var upright = new RenderMatrix(1, 0, 0, -1, 0, 0).Then(frame);

        _body.Append("<text xml:space=\"preserve\" transform=\"");
        SvgText.AppendMatrix(_body, upright);
        _body.Append("\" font-family=\"").Append(font.Family).Append("\" font-size=\"").Append(SvgText.Number(Math.Abs(size))).Append('"');
        if (font.IsBold) _body.Append(" font-weight=\"bold\"");
        if (font.IsItalic) _body.Append(" font-style=\"italic\"");
        _body.Append(" font-variant=\"no-common-ligatures no-discretionary-ligatures\" x=\"").Append(positions).Append("\" y=\"0\"");

        switch (_state.RenderMode)
        {
            case 0: AppendFill(); break;
            case 1: _body.Append(" fill=\"none\""); AppendTextStroke(); break;
            case 2: AppendFill(); AppendTextStroke(); break;
            default: _body.Append(" fill=\"#000000\" fill-opacity=\"0\""); break; // invisible, still selectable
        }

        _body.Append('>');
        SvgText.AppendEscaped(_body, characters.ToString());
        _body.Append("</text>");
    }

    // Draws glyph outlines: each glyph's path is defined once and placed with its own matrix.
    private void DrawGlyphs(EmbeddedRenderFont font, List<(int Glyph, double X)> placed)
    {
        if (_state.RenderMode == 3 || placed.Count == 0) return; // invisible text draws nothing

        int number = _fontNumbers[font];
        double size = _state.FontSize;
        double scale = font.UnitsToEm * size;
        var frame = new RenderMatrix(_state.HorizontalScale, 0, 0, 1, 0, _state.Rise).Then(_textMatrix).Then(_state.Ctm);

        _body.Append("<g");
        if (_state.RenderMode == 1) _body.Append(" fill=\"none\""); else AppendFill();
        if (_state.RenderMode is 1 or 2)
        {
            var textToUser = new RenderMatrix(_state.HorizontalScale, 0, 0, 1, 0, 0).Then(_textMatrix);
            double userScale = Math.Sqrt(Math.Abs(textToUser.A * textToUser.D - textToUser.B * textToUser.C)) * Math.Abs(scale);
            _body.Append(" stroke=\"");
            SvgText.AppendColor(_body, _state.Stroke.Red, _state.Stroke.Green, _state.Stroke.Blue);
            if (_state.LineWidth <= 0 || userScale <= 0)
                _body.Append("\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\""); // the thinnest line, as for paths
            else
                _body.Append("\" stroke-width=\"").Append(SvgText.Number(_state.LineWidth / userScale)).Append('"');
        }
        _body.Append('>');

        foreach (var (glyph, x) in placed)
        {
            string outline = font.Outline(glyph, Math.Max(0, _limits.MaxOutputCharacters - _body.Length - _defs.Length), ref _glyphComponents, _limits.MaxGlyphComponents);
            if (outline.Length == 0) continue;
            string id = $"{_idPrefix}f{number}g{glyph}";
            if (_glyphDefinitions.Add((number, glyph)))
            {
                _defs.Append("<path id=\"").Append(id).Append("\" d=\"").Append(outline).Append("\"/>");
                CheckOutput();
            }
            _body.Append("<use href=\"#").Append(id).Append("\" transform=\"");
            SvgText.AppendMatrix(_body, new RenderMatrix(scale, 0, 0, scale, x, 0).Then(frame));
            _body.Append("\"/>");
            CheckOutput();
        }
        _body.Append("</g>");
    }

    // An invisible run of real text over glyph outlines, stretched to the run's width, so the page's text can be
    // selected, found, and read aloud (WCAG 2.2 1.1.1, 1.4.5).
    private void DrawTextLayer(string text, double width)
    {
        double size = _state.FontSize;
        double sign = size < 0 ? -1 : 1;
        var frame = new RenderMatrix(_state.HorizontalScale * sign, 0, 0, sign, 0, _state.Rise).Then(_textMatrix).Then(_state.Ctm);
        _body.Append("<text xml:space=\"preserve\" transform=\"");
        SvgText.AppendMatrix(_body, new RenderMatrix(1, 0, 0, -1, 0, 0).Then(frame));
        _body.Append("\" font-family=\"sans-serif\" font-size=\"").Append(SvgText.Number(Math.Abs(size))).Append('"');
        if (width * sign > 0)
            _body.Append(" textLength=\"").Append(SvgText.Number(width * sign)).Append("\" lengthAdjust=\"spacingAndGlyphs\"");
        _body.Append(" fill=\"#000000\" fill-opacity=\"0\">");
        SvgText.AppendEscaped(_body, text);
        _body.Append("</text>");
    }

    private void AppendFill()
    {
        _body.Append(" fill=\"");
        SvgText.AppendColor(_body, _state.Fill.Red, _state.Fill.Green, _state.Fill.Blue);
        _body.Append('"');
    }

    // The line width is in user space; the text element's units are text space, so it is scaled back.
    private void AppendTextStroke()
    {
        var textToUser = new RenderMatrix(_state.HorizontalScale, 0, 0, 1, 0, 0).Then(_textMatrix);
        double scale = Math.Sqrt(Math.Abs(textToUser.A * textToUser.D - textToUser.B * textToUser.C));
        _body.Append(" stroke=\"");
        SvgText.AppendColor(_body, _state.Stroke.Red, _state.Stroke.Green, _state.Stroke.Blue);
        if (_state.LineWidth <= 0 || scale <= 0)
            _body.Append("\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\""); // the thinnest line, as for paths
        else
            _body.Append("\" stroke-width=\"").Append(SvgText.Number(_state.LineWidth / scale)).Append('"');
    }

    // Drops characters XML can't hold (control codes, unpaired surrogates) and turns tabs and line breaks into spaces,
    // so the drawn text, its positions, and the text layer stay in step and each text object stays one line.
    private static string XmlSafe(string text)
    {
        bool safe = true;
        for (int i = 0; i < text.Length && safe; i++) safe = IsXmlCharacter(text, i) && text[i] is not ('\t' or '\n' or '\r');
        if (safe) return text;
        var kept = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { kept.Append(text[i]).Append(text[++i]); continue; }
            if (text[i] is '\t' or '\n' or '\r') kept.Append(' ');
            else if (IsXmlCharacter(text, i)) kept.Append(text[i]);
        }
        return kept.ToString();
    }

    private static bool IsXmlCharacter(string text, int i)
    {
        char c = text[i];
        if (char.IsHighSurrogate(c)) return i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
        if (char.IsLowSurrogate(c)) return i > 0 && char.IsHighSurrogate(text[i - 1]);
        return c is '\t' or '\n' or '\r' || (c >= 0x20 && c <= 0xFFFD && c is not ('\uFFFE' or '\uFFFF'));
    }

    private static byte[] StringOperand(IReadOnlyList<PdfReadValue> operands, int fromEnd) =>
        operands.Count >= fromEnd && operands[^fromEnd] is PdfReadString text
            ? text.Bytes
            : throw new PageRenderException("A text operator has no string.");

    #endregion

    #endregion

    #region Nested Types

    // A marked-content sequence: its /ActualText, and whether that text has been placed in the page's text yet.
    private sealed class MarkedSpan
    {
        public MarkedSpan(string? actualText) => ActualText = actualText;

        public string? ActualText { get; }

        public bool Shown { get; set; }
    }

    #endregion

    #region Properties, Indexers

    private IReadOnlyList<string> TextLines
    {
        get
        {
            // A text object left open at the end of the page still contributes its line.
            // Text left after the last ET (an open text object, or an /ActualText outside one) still makes a line.
            var lines = new List<string>(_textLines);
            if (_line.Length > 0) lines.Add(_line.ToString());
            return lines;
        }
    }

    #endregion
}
